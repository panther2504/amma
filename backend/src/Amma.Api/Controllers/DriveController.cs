using System.Net;
using System.Security.Cryptography;
using System.Text;
using Amma.Api.Auth;
using Amma.Api.Services;
using Amma.Domain.Documents;
using Amma.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amma.Api.Controllers;

public record DriveStatus(bool Configured, bool Connected, string? Email, string? ClientId, int PhotosInDrive, int PhotosOnServer, string RedirectUri);
public record DriveConfigRequest(string ClientId, string ClientSecret);
public record MoveResult(int Moved, int Remaining);

/// <summary>Connects the family's Google Drive for full-size memory photos.</summary>
[Route("api/v1/drive")]
public class DriveController(GoogleDrive drive, IFileStorage storage, TokenService tokens, IOptions<AppUrlOptions> urls, ILogger<DriveController> log) : ApiControllerBase
{
    private string Redirect => $"{(string.IsNullOrEmpty(urls.Value.PublicUrl) ? $"{Request.Scheme}://{Request.Host}" : urls.Value.PublicUrl.TrimEnd('/'))}/api/v1/drive/callback";

    [HttpGet]
    public async Task<DriveStatus> Status(CancellationToken ct)
    {
        var c = await drive.GetAsync(UserId, ct);
        var photos = await Db.MemoryPhotos.AsNoTracking().Where(m => m.UserId == UserId).Select(m => m.PhotoKey).ToListAsync(ct);
        var inDrive = photos.Count(k => k.StartsWith(GoogleDrive.KeyPrefix));
        return new DriveStatus(c is not null, GoogleDrive.IsConnected(c), c?.Email, c?.ClientId, inDrive, photos.Count - inDrive, Redirect);
    }

    /// <summary>Saves the Google app's client ID and secret (entered on the phone; stored encrypted).</summary>
    [HttpPut("config")]
    public async Task<IActionResult> Config(DriveConfigRequest req, CancellationToken ct)
    {
        var id = req.ClientId.Trim(); var secret = req.ClientSecret.Trim();
        if (!id.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal) || id.Length > 200) return Problem422("The Client ID should end with .apps.googleusercontent.com");
        if (secret.Length is < 10 or > 200) return Problem422("Please paste the Client secret.");
        var c = await drive.GetAsync(UserId, ct);
        if (c is null) Db.DriveConnections.Add(c = new DriveConnection { UserId = UserId });
        if (c.ClientId != id) { c.RefreshToken = null; c.FolderId = null; c.Email = null; c.ConnectedAt = null; }
        c.ClientId = id; c.ClientSecret = drive.Protect(secret);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>The Google sign-in link to open in the phone's browser.</summary>
    [HttpGet("connect-url")]
    public async Task<ActionResult<object>> ConnectUrl(CancellationToken ct)
    {
        var c = await drive.GetAsync(UserId, ct);
        if (c is null) return Problem422("Enter the Google Client ID and secret first.");
        var exp = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds();
        var payload = $"{UserId:N}.{exp}";
        return new { url = GoogleDrive.AuthUrl(c, Redirect, $"{payload}.{tokens.Hash("drive:" + payload)[..32]}") };
    }

    /// <summary>Google sends the browser back here after sign-in.</summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<ContentResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error)) return Page("Google Drive was not connected", $"Google said: {error}. Go back to Amma and try again.", false);
        var parts = (state ?? "").Split('.');
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var userId) || !long.TryParse(parts[1], out var exp)
            || exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(tokens.Hash($"drive:{parts[0]}.{parts[1]}")[..32]), Encoding.UTF8.GetBytes(parts[2]))
            || string.IsNullOrEmpty(code))
            return Page("This link has expired", "Go back to Amma and tap Connect Google Drive again.", false);

        var c = await drive.GetAsync(userId, ct);
        if (c is null) return Page("Google Drive was not connected", "Go back to Amma and enter the Client ID and secret first.", false);
        try
        {
            await drive.ConnectAsync(c, code, Redirect, ct);
            return Page("Google Drive connected ✓", $"New memory photos will be saved in the \"Amma Memories\" folder{(c.Email is null ? "" : $" of {c.Email}")}. You can go back to Amma now.", true);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Drive connect failed");
            return Page("Google Drive was not connected", ex.Message, false);
        }
    }

    /// <summary>Moves photos stored on the server into Google Drive, a few at a time (call until Remaining is 0).</summary>
    [HttpPost("move-existing")]
    public async Task<ActionResult<MoveResult>> MoveExisting(CancellationToken ct)
    {
        var c = await drive.GetAsync(UserId, ct);
        if (!GoogleDrive.IsConnected(c)) return Problem422("Connect Google Drive first.");
        var batch = await Db.MemoryPhotos.Where(m => m.UserId == UserId && !m.PhotoKey.StartsWith(GoogleDrive.KeyPrefix)).OrderBy(m => m.CreatedAt).Take(5).ToListAsync(ct);
        var moved = 0;
        foreach (var m in batch)
        {
            await using var s = await storage.OpenAsync(m.PhotoKey, ct);
            var id = await drive.UploadAsync(c!, s, $"{m.TakenOn:yyyy-MM-dd} {m.Caption ?? "Amma"}.jpg", ct);
            var old = m.PhotoKey;
            m.PhotoKey = GoogleDrive.KeyPrefix + id;
            await Db.SaveChangesAsync(ct);
            await storage.DeleteAsync(old, ct);
            moved++;
        }
        var remaining = await Db.MemoryPhotos.CountAsync(m => m.UserId == UserId && !m.PhotoKey.StartsWith(GoogleDrive.KeyPrefix), ct);
        return new MoveResult(moved, remaining);
    }

    /// <summary>Stops using Google Drive for new photos (photos already in Drive stay there).</summary>
    [HttpDelete]
    public async Task<IActionResult> Disconnect(CancellationToken ct)
    {
        var c = await drive.GetAsync(UserId, ct);
        if (c is not null) { c.RefreshToken = null; c.ConnectedAt = null; await Db.SaveChangesAsync(ct); }
        return NoContent();
    }

    private static ContentResult Page(string title, string message, bool ok) => new()
    {
        ContentType = "text/html; charset=utf-8",
        StatusCode = 200,
        Content = $$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Amma · Google Drive</title>
            <style>body{margin:0;min-height:100vh;display:grid;place-items:center;background:#FBF6F2;color:#2B1A26;font-family:system-ui,sans-serif}
            .c{max-width:360px;margin:24px;padding:28px;border-radius:24px;background:#fff;box-shadow:0 12px 32px -14px rgba(94,31,71,.3);text-align:center}
            h1{font-size:22px;margin:12px 0 8px}p{color:#5C4A57;line-height:1.55}.e{font-size:54px}</style></head>
            <body><div class="c"><div class="e">{{(ok ? "📸" : "⚠️")}}</div><h1>{{WebUtility.HtmlEncode(title)}}</h1><p>{{WebUtility.HtmlEncode(message)}}</p></div></body></html>
            """,
    };
}
