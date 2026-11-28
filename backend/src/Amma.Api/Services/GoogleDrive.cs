using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Amma.Domain.Documents;
using Amma.Infrastructure.Persistence;
using Amma.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Amma.Api.Services;

public class AppUrlOptions
{
    /// <summary>Public address of this server, used for the Google sign-in return link.</summary>
    public string PublicUrl { get; set; } = "";
}

/// <summary>Minimal Google Drive client (OAuth + drive.file uploads) for memory photos.</summary>
public class GoogleDrive(AmmaDbContext db, FileCrypto crypto, IHttpClientFactory http, IMemoryCache cache, ILogger<GoogleDrive> log)
{
    public const string Scope = "https://www.googleapis.com/auth/drive.file";
    public const string KeyPrefix = "gdrive:";
    private HttpClient Http => http.CreateClient("google");

    public Task<DriveConnection?> GetAsync(Guid userId, CancellationToken ct) => db.DriveConnections.FirstOrDefaultAsync(d => d.UserId == userId, ct);

    public static bool IsConnected(DriveConnection? c) => c?.RefreshToken is not null && c.FolderId is not null;

    public string Secret(DriveConnection c) => Encoding.UTF8.GetString(crypto.Unprotect(c.ClientSecret));
    public byte[] Protect(string value) => crypto.Protect(Encoding.UTF8.GetBytes(value));

    public static string AuthUrl(DriveConnection c, string redirect, string state) =>
        "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join("&", new Dictionary<string, string>
        {
            ["client_id"] = c.ClientId, ["redirect_uri"] = redirect, ["response_type"] = "code", ["scope"] = Scope,
            ["access_type"] = "offline", ["prompt"] = "consent", ["include_granted_scopes"] = "true", ["state"] = state,
        }.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));

    /// <summary>Exchanges the sign-in code, creates the "Amma Memories" folder and remembers the account.</summary>
    public async Task ConnectAsync(DriveConnection c, string code, string redirect, CancellationToken ct)
    {
        using var res = await Http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code, ["client_id"] = c.ClientId, ["client_secret"] = Secret(c), ["redirect_uri"] = redirect, ["grant_type"] = "authorization_code",
        }), ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Google sign-in failed: {Short(json)}");
        using var doc = JsonDocument.Parse(json);
        var refresh = doc.RootElement.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        if (string.IsNullOrEmpty(refresh)) throw new InvalidOperationException("Google did not grant offline access. Please try connecting again.");
        var access = doc.RootElement.GetProperty("access_token").GetString()!;
        cache.Set(CacheKey(c), access, TimeSpan.FromSeconds(Math.Max(60, doc.RootElement.GetProperty("expires_in").GetInt32() - 120)));

        c.RefreshToken = crypto.Protect(Encoding.UTF8.GetBytes(refresh));
        c.Email = await EmailAsync(access, ct);
        c.FolderId = await CreateFolderAsync(access, ct);
        c.ConnectedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<string> UploadAsync(DriveConnection c, Stream content, string name, CancellationToken ct)
    {
        var meta = JsonSerializer.Serialize(new { name, parents = new[] { c.FolderId }, mimeType = "image/jpeg" });
        using var body = new MultipartContent("related");
        body.Add(new StringContent(meta, Encoding.UTF8, "application/json"));
        var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        body.Add(file);
        using var res = await SendAsync(c, () => new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id") { Content = body }, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Drive upload failed: {Short(json)}");
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<Stream> DownloadAsync(DriveConnection c, string fileId, CancellationToken ct)
    {
        var res = await SendAsync(c, () => new HttpRequestMessage(HttpMethod.Get, $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}?alt=media"), ct);
        if (!res.IsSuccessStatusCode) { res.Dispose(); throw new InvalidOperationException("The photo could not be loaded from Google Drive."); }
        var ms = new MemoryStream();
        await res.Content.CopyToAsync(ms, ct);
        res.Dispose();
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(DriveConnection c, string fileId, CancellationToken ct)
    {
        using var res = await SendAsync(c, () => new HttpRequestMessage(HttpMethod.Delete, $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}"), ct);
        if (!res.IsSuccessStatusCode && (int)res.StatusCode != 404) log.LogWarning("Drive delete failed: {Status}", res.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(DriveConnection c, Func<HttpRequestMessage> make, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var req = make();
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessTokenAsync(c, attempt > 0, ct));
            var res = await Http.SendAsync(req, ct);
            if ((int)res.StatusCode != 401 || attempt > 0) return res;
            res.Dispose();
        }
    }

    private async Task<string> AccessTokenAsync(DriveConnection c, bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh && cache.TryGetValue(CacheKey(c), out string? token) && token is not null) return token;
        if (c.RefreshToken is null) throw new InvalidOperationException("Google Drive is not connected.");
        using var res = await Http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = c.ClientId, ["client_secret"] = Secret(c), ["grant_type"] = "refresh_token",
            ["refresh_token"] = Encoding.UTF8.GetString(crypto.Unprotect(c.RefreshToken)),
        }), ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Google Drive access expired — please reconnect in Settings. ({Short(json)})");
        using var doc = JsonDocument.Parse(json);
        var access = doc.RootElement.GetProperty("access_token").GetString()!;
        cache.Set(CacheKey(c), access, TimeSpan.FromSeconds(Math.Max(60, doc.RootElement.GetProperty("expires_in").GetInt32() - 120)));
        return access;
    }

    private async Task<string> CreateFolderAsync(string access, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/drive/v3/files?fields=id")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { name = "Amma Memories", mimeType = "application/vnd.google-apps.folder" }), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using var res = await Http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Could not create the Drive folder: {Short(json)}");
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<string?> EmailAsync(string access, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/drive/v3/about?fields=user(emailAddress)");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using var res = await Http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.TryGetProperty("user", out var u) && u.TryGetProperty("emailAddress", out var e) ? e.GetString() : null;
    }

    private static string CacheKey(DriveConnection c) => $"gdrive-token:{c.UserId}";
    private static string Short(string s) => s.Length > 200 ? s[..200] : s;
}
