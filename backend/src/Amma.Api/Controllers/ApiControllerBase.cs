using Amma.Api.Auth;
using Amma.Api.Services;
using Amma.Domain.Pregnancy;
using Amma.Domain.Users;
using Amma.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid UserId => User.UserId();

    protected AmmaDbContext Db => HttpContext.RequestServices.GetRequiredService<AmmaDbContext>();
    protected UserClock Clock => HttpContext.RequestServices.GetRequiredService<UserClock>();

    private User? _me;
    protected async Task<User> MeAsync(CancellationToken ct) =>
        _me ??= await Db.Users.FirstOrDefaultAsync(u => u.Id == UserId && u.IsActive, ct) ?? throw new UnauthorizedAccessException();

    protected async Task<DateOnly> TodayAsync(CancellationToken ct) => Clock.Today((await MeAsync(ct)).TimeZone);

    protected Task<PregnancyProfile?> ActivePregnancyAsync(CancellationToken ct, bool track = false)
    {
        var q = track ? Db.PregnancyProfiles : Db.PregnancyProfiles.AsNoTracking();
        return q.Where(p => p.UserId == UserId && p.Status == PregnancyStatus.Active).OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(ct);
    }

    /// <summary>The login (phone) making this request, when signed in with a PIN.</summary>
    protected Guid? LoginId => Guid.TryParse(User.FindFirst(Auth.TokenService.LoginClaim)?.Value, out var id) ? id : null;

    /// <summary>Name of the person on this phone (falls back to the account name).</summary>
    protected async Task<string> PersonNameAsync(CancellationToken ct)
    {
        if (LoginId is { } lid)
        {
            var n = await Db.LoginIdentities.AsNoTracking().Where(l => l.Id == lid).Select(l => l.DisplayName).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(n)) return n;
        }
        return (await MeAsync(ct)).Name;
    }

    protected ObjectResult Problem422(string detail) => Problem(detail: detail, statusCode: StatusCodes.Status422UnprocessableEntity);
}
