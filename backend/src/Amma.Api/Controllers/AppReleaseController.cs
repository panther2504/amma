using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Amma.Api.Controllers;

public class AppReleaseOptions
{
    /// <summary>GitHub repository that publishes the Android app ("android-latest" release).</summary>
    public string GitHubRepo { get; set; } = "panther2504/amma";
    /// <summary>
    /// Optional: a base64 PKCS#12 keystore + password to sign the APK. When not set, the server generates its own key
    /// once and keeps it (encrypted) in the database. Either way every update is signed with the same key.
    /// </summary>
    public string? KeystoreBase64 { get; set; }
    public string? KeystorePassword { get; set; }
}

public record AndroidRelease(int VersionCode, string VersionName, string DownloadUrl, string? Notes, DateTimeOffset? PublishedAt);
public record SigningKeyResponse(string KeystoreBase64, string Password);

[ApiController]
[AllowAnonymous]
[Route("api/v1")]
public partial class AppReleaseController(IHttpClientFactory http, IMemoryCache cache, IOptions<AppReleaseOptions> options, AndroidSigningKeys keys, ILogger<AppReleaseController> log) : ControllerBase
{
    private readonly AppReleaseOptions _o = options.Value;

    /// <summary>The newest Android app version, used by the app's "Update available" check.</summary>
    [HttpGet("app/android")]
    public async Task<ActionResult<AndroidRelease>> LatestAndroid(CancellationToken ct)
    {
        var release = await cache.GetOrCreateAsync("android-release", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            try
            {
                var client = http.CreateClient("github");
                using var res = await client.GetAsync($"https://api.github.com/repos/{_o.GitHubRepo}/releases/tags/android-latest", ct);
                if (!res.IsSuccessStatusCode) { e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1); return null; }
                return ParseRelease(await res.Content.ReadAsStringAsync(ct));
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Could not read the Android release from GitHub");
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return null;
            }
        });
        return release is null ? NotFound() : release;
    }

    /// <summary>Reads version and download link from the GitHub release JSON (the notes contain "versionCode: N").</summary>
    public static AndroidRelease? ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        var m = VersionCode().Match(body);
        if (!m.Success) return null;
        string? url = null;
        if (root.TryGetProperty("assets", out var assets))
            foreach (var a in assets.EnumerateArray())
                if (a.GetProperty("name").GetString()?.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) == true)
                    url = a.GetProperty("browser_download_url").GetString();
        if (url is null) return null;
        var name = VersionName().Match(body) is { Success: true } n ? n.Groups[1].Value : $"1.0.{m.Groups[1].Value}";
        var notes = body.Split('\n').FirstOrDefault(l => !l.Contains("versionCode") && !l.Contains("versionName") && l.Trim().Length > 0)?.Trim();
        DateTimeOffset? published = root.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String ? p.GetDateTimeOffset() : null;
        return new AndroidRelease(int.Parse(m.Groups[1].Value), name, url, notes, published);
    }

    /// <summary>
    /// Gives the APK signing key to this repository's GitHub Actions build. The caller must present a GitHub
    /// OIDC token (no stored secret needed): we check GitHub's signature, the audience and the repository.
    /// </summary>
    [HttpPost("ci/android-signing-key")]
    public async Task<ActionResult<SigningKeyResponse>> SigningKey(CancellationToken ct)
    {
        var auth = Request.Headers.Authorization.ToString();
        if (!auth.StartsWith("Bearer ", StringComparison.Ordinal)) return Unauthorized();

        var config = await GitHubOidc.GetConfigurationAsync(ct);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(auth["Bearer ".Length..], new TokenValidationParameters
        {
            ValidIssuer = "https://token.actions.githubusercontent.com",
            ValidAudience = "amma-signing",
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        });
        if (!result.IsValid) { log.LogWarning(result.Exception, "Rejected signing-key request: invalid OIDC token"); return Unauthorized(); }
        var repo = result.Claims.TryGetValue("repository", out var r) ? r?.ToString() : null;
        if (!string.Equals(repo, _o.GitHubRepo, StringComparison.OrdinalIgnoreCase))
        {
            log.LogWarning("Rejected signing-key request from repository {Repo}", repo);
            return Forbid();
        }
        log.LogInformation("Issued Android signing key to {Repo} workflow run {Run}", repo, (result.Claims.TryGetValue("run_id", out var run) ? run : null));
        return await keys.GetAsync(ct);
    }

    private static readonly ConfigurationManager<OpenIdConnectConfiguration> GitHubOidc = new(
        "https://token.actions.githubusercontent.com/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever { RequireHttps = true });

    [GeneratedRegex(@"versionCode:\s*(\d+)")] private static partial Regex VersionCode();
    [GeneratedRegex(@"versionName:\s*([\w.\-]+)")] private static partial Regex VersionName();
}

/// <summary>Provides the APK signing key: from configuration, or generated once and stored encrypted in the database.</summary>
public class AndroidSigningKeys(Amma.Infrastructure.Persistence.AmmaDbContext db, Amma.Infrastructure.Storage.FileCrypto crypto, IOptions<AppReleaseOptions> options, ILogger<AndroidSigningKeys> log)
{
    private const string Name = "android-signing-key";

    public async Task<SigningKeyResponse> GetAsync(CancellationToken ct)
    {
        var o = options.Value;
        if (!string.IsNullOrEmpty(o.KeystoreBase64) && !string.IsNullOrEmpty(o.KeystorePassword))
            return new SigningKeyResponse(o.KeystoreBase64, o.KeystorePassword);

        var stored = await db.AppSecrets.AsNoTracking().FirstOrDefaultAsync(s => s.Name == Name, ct);
        if (stored is not null) return Decode(stored);

        var generated = Generate();
        db.AppSecrets.Add(new Amma.Infrastructure.Persistence.AppSecret
        {
            Name = Name,
            Value = crypto.Protect(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(generated))),
        });
        try
        {
            await db.SaveChangesAsync(ct);
            log.LogInformation("Generated a new Android signing key");
            return generated;
        }
        catch (DbUpdateException)
        {
            // Another request created it at the same moment: use that one.
            db.ChangeTracker.Clear();
            return Decode(await db.AppSecrets.AsNoTracking().FirstAsync(s => s.Name == Name, ct));
        }
    }

    private SigningKeyResponse Decode(Amma.Infrastructure.Persistence.AppSecret s) =>
        JsonSerializer.Deserialize<SigningKeyResponse>(crypto.Unprotect(s.Value))!;

    /// <summary>RSA-3072 self-signed certificate valid for 30 years, exported as a password-protected PKCS#12 keystore.</summary>
    public static SigningKeyResponse Generate()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(3072);
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=Amma, O=Family", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(30));
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(18)).ToLowerInvariant();
        var pfx = cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pkcs12, password);
        return new SigningKeyResponse(Convert.ToBase64String(pfx), password);
    }
}
