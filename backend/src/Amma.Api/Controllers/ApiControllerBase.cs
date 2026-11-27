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

    protected ObjectResult Problem422(string detail) => Problem(detail: detail, statusCode: StatusCodes.Status422UnprocessableEntity);
}
