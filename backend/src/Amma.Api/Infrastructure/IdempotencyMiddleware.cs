using System.Security.Claims;
using Amma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Infrastructure;

/// <summary>
/// Honors the Idempotency-Key header on writes from signed-in users. The offline outbox in the app
/// replays queued changes with the same key, so a change is applied exactly once even if a previous
/// attempt reached the server but the response was lost.
/// </summary>
public class IdempotencyMiddleware(RequestDelegate next)
{
    private const int MaxBody = 64 * 1024;

    public async Task InvokeAsync(HttpContext ctx, AmmaDbContext db)
    {
        var key = ctx.Request.Headers["Idempotency-Key"].ToString();
        var sub = ctx.User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(key) || key.Length > 100 || HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method) || !Guid.TryParse(sub, out var userId))
        {
            await next(ctx);
            return;
        }

        var existing = await db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key, ctx.RequestAborted);
        if (existing is not null)
        {
            ctx.Response.StatusCode = existing.StatusCode;
            ctx.Response.Headers["Idempotent-Replayed"] = "true";
            if (existing.ContentType is not null) ctx.Response.ContentType = existing.ContentType;
            if (existing.Body is not null) await ctx.Response.WriteAsync(existing.Body, ctx.RequestAborted);
            return;
        }

        var original = ctx.Response.Body;
        await using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;
        try
        {
            await next(ctx);
        }
        finally
        {
            ctx.Response.Body = original;
        }

        // Only remember outcomes that should not be retried (success or a validation failure).
        if (ctx.Response.StatusCode < 500 && ctx.Response.StatusCode != 401 && ctx.Response.StatusCode != 429)
        {
            string? body = null;
            if (buffer.Length <= MaxBody && (ctx.Response.ContentType?.Contains("json") ?? false))
                body = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
            db.ChangeTracker.Clear();
            db.IdempotencyRecords.Add(new IdempotencyRecord { UserId = userId, Key = key, StatusCode = ctx.Response.StatusCode, ContentType = ctx.Response.ContentType, Body = body });
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { /* concurrent duplicate: first one wins */ }
        }
        buffer.Position = 0;
        await buffer.CopyToAsync(original);
    }
}
