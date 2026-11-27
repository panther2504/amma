using System.Security.Cryptography;
using Amma.Api.Auth;
using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Family;
using Amma.Domain.Pregnancy;
using Amma.Domain.Users;
using Amma.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
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
public class AuthController(AmmaDbContext db, TokenService tokens, IOtpSender sender, IOptions<OtpOptions> otpOptions, AuditService audit, TimeProvider clock) : ControllerBase
{
    private readonly OtpOptions _otp = otpOptions.Value;

    [HttpPost("otp/request")]
    public async Task<ActionResult<OtpRequestResponse>> RequestOtp(OtpRequest req, CancellationToken ct)
    {
        var dest = Destination.Normalize(req.Destination);
        if (dest is null) return ValidationProblem(new ValidationProblemDetails(Msg("Destination", "Enter a valid Indian mobile number (+91) or e-mail address.")));

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
        var dest = Destination.Normalize(req.Destination);
        if (dest is null) return ValidationProblem(new ValidationProblemDetails(Msg("Destination", "Invalid mobile number or e-mail.")));
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

        var isEmail = Destination.IsEmail(dest);
        var user = await db.Users.FirstOrDefaultAsync(u => isEmail ? u.Email == dest : u.Phone == dest, ct);
        var isNew = user is null;
        if (user is null)
        {
            user = new User { Email = isEmail ? dest : null, Phone = isEmail ? null : dest };
            db.Users.Add(user);
        }
        if (!user.IsActive) return Problem(detail: "This account is disabled.", statusCode: 403);
        if (_otp.AdminDestinations.Contains(dest, StringComparer.OrdinalIgnoreCase)) user.Role = UserRole.Admin;
        user.LastLoginAt = now;

        // Accept pending family invitations sent to this phone number.
        if (!isEmail)
        {
            var invites = await db.FamilyMembers.Where(f => f.Phone == dest && f.Status == FamilyMemberStatus.Invited && f.UserId != user.Id).ToListAsync(ct);
            foreach (var inv in invites) { inv.MemberUserId = user.Id; inv.Status = FamilyMemberStatus.Active; }
            if (isNew && invites.Count > 0) user.Role = invites[0].Role == FamilyRole.Partner ? UserRole.Partner : UserRole.FamilyViewer;
        }

        audit.Record(user.Id, isNew ? "auth.register" : "auth.login");
        var response = await IssueAsync(user, isNew, ct);
        return response;
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
