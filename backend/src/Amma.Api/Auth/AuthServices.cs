using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Amma.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Amma.Api.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "amma";
    public string Audience { get; set; } = "amma-app";
    /// <summary>At least 32 characters. MUST come from a secret store in production.</summary>
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
}

public class OtpOptions
{
    public int Length { get; set; } = 6;
    public int ExpiryMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    /// <summary>Development only: echo the OTP in the API response so the app can be tested without SMS.</summary>
    public bool ReturnCodeInResponse { get; set; }
    /// <summary>Phones/e-mails that are promoted to Admin on first login (bootstrap only).</summary>
    public List<string> AdminDestinations { get; set; } = [];
    /// <summary>When not empty, only these phones/e-mails may sign in (private use, e.g. a single family).</summary>
    public List<string> AllowedDestinations { get; set; } = [];

    public bool IsAllowed(string normalized) =>
        AllowedDestinations.Count == 0 || AllowedDestinations.Any(a => Destination.Normalize(a) == normalized);
}

public static partial class Destination
{
    [GeneratedRegex(@"^\+91[6-9]\d{9}$")] private static partial Regex IndianMobile();
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")] private static partial Regex Email();

    /// <summary>Normalises a phone number or e-mail. Returns null when invalid.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (s.Contains('@')) return Email().IsMatch(s) ? s.ToLowerInvariant() : null;
        var digits = new string(s.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) digits = "91" + digits;
        if (digits.Length == 12 && digits.StartsWith("91")) s = "+" + digits;
        return IndianMobile().IsMatch(s) ? s : null;
    }

    public static bool IsEmail(string normalized) => normalized.Contains('@');
}

public class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JwtOptions _o = options.Value;

    public (string token, DateTimeOffset expires) CreateAccessToken(User user)
    {
        var expires = clock.GetUtcNow().AddMinutes(_o.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("name", user.Name),
        };
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.SigningKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_o.Issuer, _o.Audience, claims, expires: expires.UtcDateTime, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public static string NewRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public string Hash(string value)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(_o.SigningKey));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    public int RefreshTokenDays => _o.RefreshTokenDays;
}

/// <summary>Delivers OTPs. Replace with an SMS (e.g. MSG91 / Twilio) and e-mail provider in production.</summary>
public interface IOtpSender
{
    Task SendAsync(string destination, string code, CancellationToken ct);
}

public class LoggingOtpSender(ILogger<LoggingOtpSender> log) : IOtpSender
{
    public Task SendAsync(string destination, string code, CancellationToken ct)
    {
        log.LogInformation("OTP for {Destination}: {Code} (development sender - no SMS sent)", Mask(destination), code);
        return Task.CompletedTask;
    }

    private static string Mask(string d) => d.Length <= 4 ? "****" : new string('*', d.Length - 4) + d[^4..];
}

public static class ClaimsExtensions
{
    public static Guid UserId(this ClaimsPrincipal p) =>
        Guid.Parse(p.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? p.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
}

public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string MedicalReviewer = nameof(UserRole.MedicalReviewer);
    public const string ContentManager = nameof(UserRole.ContentManager);
    public const string ContentEditors = Admin + "," + ContentManager + "," + MedicalReviewer;
}
