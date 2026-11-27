using System.Security.Cryptography;
using Amma.Api.Auth;
using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Family;
using Amma.Domain.Pregnancy;
using Amma.Domain.Users;
using Amma.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amma.Api.Controllers;

/// <summary>Passwordless sign-in with a one-time code sent to an Indian mobile number or e-mail.</summary>
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class AuthController(AmmaDbContext db, TokenService tokens, IOtpSender sender, IOptions<OtpOptions> otpOptions, IOptions<AmmaSignInOptions> signIn, AuditService audit, TimeProvider clock) : ControllerBase
{
    private readonly OtpOptions _otp = otpOptions.Value;
    private readonly AmmaSignInOptions _signIn = signIn.Value;
    private static readonly PasswordHasher<User> PinHasher = new();

    /// <summary>Tells the app which sign-in screen to show.</summary>
    [HttpGet("config")]
    public AuthConfigResponse Config() => new(_signIn.IsPin ? "pin" : "otp");

    [HttpPost("otp/request")]
    public async Task<ActionResult<OtpRequestResponse>> RequestOtp(OtpRequest req, CancellationToken ct)
    {
        if (_signIn.IsPin) return NotFound();
        var dest = Destination.Normalize(req.Destination);
        if (dest is null) return ValidationProblem(new ValidationProblemDetails(Msg("Destination", "Enter a valid Indian mobile number (+91) or e-mail address.")));
        if (!_otp.IsAllowed(dest)) return Problem(detail: "This is a private app. This number is not on the access list.", statusCode: 403);

        var now = clock.GetUtcNow();
        var recent = await db.OtpCodes.CountAsync(o => o.Destination == dest && o.CreatedAt > now.AddMinutes(-15), ct);
        if (recent >= 5) return Problem(detail: "Too many codes requested. Please try again in a few minutes.", statusCode: 429);

        var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, _otp.Length)).ToString().PadLeft(_otp.Length, '0');
        db.OtpCodes.Add(new OtpCode { Destination = dest, CodeHash = tokens.Hash(dest + ":" + code), Purpose = OtpPurpose.Login, ExpiresAt = now.AddMinutes(_otp.ExpiryMinutes) });
        await db.SaveChangesAsync(ct);
        await sender.SendAsync(dest, code, ct);
        return new OtpRequestResponse(true, _otp.ExpiryMinutes * 60, _otp.ReturnCodeInResponse ? code : null);
    }

    [HttpPost("otp/verify")]
    public async Task<ActionResult<AuthResponse>> Verify(OtpVerifyRequest req, CancellationToken ct)
    {
        if (_signIn.IsPin) return NotFound();
        var dest = Destination.Normalize(req.Destination);
        if (dest is null) return ValidationProblem(new ValidationProblemDetails(Msg("Destination", "Invalid mobile number or e-mail.")));
        if (!_otp.IsAllowed(dest)) return Problem(detail: "This is a private app.", statusCode: 403);
        var now = clock.GetUtcNow();
        var otp = await db.OtpCodes
            .Where(o => o.Destination == dest && o.Purpose == OtpPurpose.Login && o.ConsumedAt == null && o.ExpiresAt > now)
            .OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(ct);
        if (otp is null) return Problem(detail: "The code has expired. Please request a new one.", statusCode: 400);
        if (otp.Attempts >= _otp.MaxAttempts) return Problem(detail: "Too many attempts. Please request a new code.", statusCode: 429);

        otp.Attempts++;
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(otp.CodeHash), Convert.FromHexString(tokens.Hash(dest + ":" + req.Code.Trim()))))
        {
            await db.SaveChangesAsync(ct);
            return Problem(detail: "That code is not correct.", statusCode: 400);
        }
        otp.ConsumedAt = now;

        return await SignInAsync(dest, null, ct);
    }

    // ---------------- PIN sign-in (private use) ----------------

    [HttpPost("pin/status")]
    public async Task<ActionResult<PinStatusResponse>> PinStatus(PinStatusRequest req, CancellationToken ct)
    {
        if (!_signIn.IsPin) return NotFound();
        var dest = Destination.Normalize(req.Destination);
        if (dest is null) return ValidationProblem(new ValidationProblemDetails(Msg("Destination", "Enter a valid Indian mobile number.")));
        if (!_otp.IsAllowed(dest)) return Problem(detail: "This is a private app. This number is not on the access list.", statusCode: 403);
        var user = await FindAsync(dest, ct);
        return new PinStatusResponse(user?.PinHash is not null);
    }

    /// <summary>Creates the PIN the first time. Requires the private setup code.</summary>
    [HttpPost("pin/setup")]
    public async Task<ActionResult<AuthResponse>> PinSetup(PinSetupRequest req, CancellationToken ct)
    {
        if (!_signIn.IsPin) return NotFound();
        var dest = Destination.Normalize(req.Destination);
        if (dest is null || !_otp.IsAllowed(dest)) return Problem(detail: "This is a private app.", statusCode: 403);
        if (string.IsNullOrEmpty(_signIn.SetupCode) ||
            !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(tokens.Hash(_signIn.SetupCode)), System.Text.Encoding.UTF8.GetBytes(tokens.Hash(req.SetupCode.Trim()))))
            return Problem(detail: "The setup code is not correct.", statusCode: 400);
        var existing = await FindAsync(dest, ct);
        if (existing?.PinHash is not null) return Problem(detail: "A PIN already exists for this number. Sign in with it instead.", statusCode: 409);
        return await SignInAsync(dest, u => { u.PinHash = PinHasher.HashPassword(u, req.Pin); u.FailedPinAttempts = 0; }, ct);
    }

    [HttpPost("pin/login")]
    public async Task<ActionResult<AuthResponse>> PinLogin(PinLoginRequest req, CancellationToken ct)
    {
        if (!_signIn.IsPin) return NotFound();
        var dest = Destination.Normalize(req.Destination);
        if (dest is null || !_otp.IsAllowed(dest)) return Problem(detail: "This is a private app.", statusCode: 403);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Phone == dest || u.Email == dest, ct);
        if (user?.PinHash is null) return Problem(detail: "No PIN has been set for this number yet.", statusCode: 400);
        var now = clock.GetUtcNow();
        if (user.PinLockedUntil > now)
            return Problem(detail: $"Too many wrong attempts. Try again after {Math.Ceiling((user.PinLockedUntil.Value - now).TotalMinutes)} minutes.", statusCode: 429);

        if (PinHasher.VerifyHashedPassword(user, user.PinHash, req.Pin.Trim()) == PasswordVerificationResult.Failed)
        {
            user.FailedPinAttempts++;
            if (user.FailedPinAttempts >= _signIn.MaxPinAttempts) { user.PinLockedUntil = now.AddMinutes(_signIn.LockoutMinutes); user.FailedPinAttempts = 0; }
            audit.Record(user.Id, "auth.pin.failed");
            await db.SaveChangesAsync(ct);
            return Problem(detail: "That PIN is not correct.", statusCode: 400);
        }
        return await SignInAsync(dest, u => { u.FailedPinAttempts = 0; u.PinLockedUntil = null; }, ct);
    }

    private Task<User?> FindAsync(string dest, CancellationToken ct)
    {
        var isEmail = Destination.IsEmail(dest);
        return db.Users.FirstOrDefaultAsync(u => isEmail ? u.Email == dest : u.Phone == dest, ct);
    }

    /// <summary>Finds or creates the user for a verified destination and issues tokens.</summary>
    private async Task<ActionResult<AuthResponse>> SignInAsync(string dest, Action<User>? apply, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var isEmail = Destination.IsEmail(dest);
        var user = await FindAsync(dest, ct);
        var isNew = user is null;
        if (user is null)
        {
            user = new User { Email = isEmail ? dest : null, Phone = isEmail ? null : dest };
            db.Users.Add(user);
        }
        if (!user.IsActive) return Problem(detail: "This account is disabled.", statusCode: 403);
        if (_otp.AdminDestinations.Contains(dest, StringComparer.OrdinalIgnoreCase)) user.Role = UserRole.Admin;
        user.LastLoginAt = now;
        apply?.Invoke(user);

        // Accept pending family invitations sent to this phone number.
        if (!isEmail)
        {
            var invites = await db.FamilyMembers.Where(f => f.Phone == dest && f.Status == FamilyMemberStatus.Invited && f.UserId != user.Id).ToListAsync(ct);
            foreach (var inv in invites) { inv.MemberUserId = user.Id; inv.Status = FamilyMemberStatus.Active; }
            if (isNew && invites.Count > 0) user.Role = invites[0].Role == FamilyRole.Partner ? UserRole.Partner : UserRole.FamilyViewer;
        }

        audit.Record(user.Id, isNew ? "auth.register" : "auth.login");
        return await IssueAsync(user, isNew, ct);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req, CancellationToken ct)
    {
        var hash = tokens.Hash(req.RefreshToken);
        var rt = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (rt is null) return Unauthorized();
        if (!rt.IsActive)
        {
            // Re-use of a rotated token: revoke the whole family of tokens for safety.
            if (rt.ReplacedById is not null)
                await db.RefreshTokens.Where(t => t.UserId == rt.UserId && t.RevokedAt == null).ExecuteUpdateAsyncSafe(db, t => t.RevokedAt = clock.GetUtcNow(), ct);
            return Unauthorized();
        }
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == rt.UserId && u.IsActive, ct);
        if (user is null) return Unauthorized();
        rt.RevokedAt = clock.GetUtcNow();
        var res = await IssueAsync(user, false, ct, rt);
        return res;
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req, CancellationToken ct)
    {
        var hash = tokens.Hash(req.RefreshToken);
        var rt = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (rt is not null && rt.RevokedAt is null) { rt.RevokedAt = clock.GetUtcNow(); await db.SaveChangesAsync(ct); }
        return NoContent();
    }

    private async Task<AuthResponse> IssueAsync(User user, bool isNew, CancellationToken ct, RefreshToken? replaces = null)
    {
        var refresh = TokenService.NewRefreshToken();
        var rt = new RefreshToken { UserId = user.Id, TokenHash = tokens.Hash(refresh), ExpiresAt = clock.GetUtcNow().AddDays(tokens.RefreshTokenDays) };
        db.RefreshTokens.Add(rt);
        if (replaces is not null) replaces.ReplacedById = rt.Id;
        await db.SaveChangesAsync(ct);
        var (access, exp) = tokens.CreateAccessToken(user);
        var hasPregnancy = await db.PregnancyProfiles.AnyAsync(p => p.UserId == user.Id && p.Status == PregnancyStatus.Active, ct);
        return new AuthResponse(access, exp, refresh, user.ToDto(hasPregnancy), isNew);
    }

    private static Dictionary<string, string[]> Msg(string key, string msg) => new() { [key] = [msg] };
}

internal static class QueryableExtensions
{
    /// <summary>ExecuteUpdate is not supported by the in-memory provider; fall back to tracked updates.</summary>
    public static async Task ExecuteUpdateAsyncSafe<T>(this IQueryable<T> q, AmmaDbContext db, Action<T> apply, CancellationToken ct) where T : class
    {
        foreach (var e in await q.ToListAsync(ct)) apply(e);
        await db.SaveChangesAsync(ct);
    }
}
