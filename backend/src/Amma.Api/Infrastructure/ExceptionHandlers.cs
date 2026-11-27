using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Amma.Api.Infrastructure;

/// <summary>A valid token for a user who no longer exists (deleted / disabled) is treated as unauthenticated.</summary>
public class UnauthorizedExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        if (ex is not UnauthorizedAccessException) return false;
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = new ProblemDetails { Status = 401, Title = "Please sign in again." },
        });
    }
}
