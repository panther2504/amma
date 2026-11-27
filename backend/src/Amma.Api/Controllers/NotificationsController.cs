using Amma.Api.Contracts;
using Amma.Domain.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

[Route("api/v1/notifications")]
public class NotificationsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<NotificationDto>> List([FromQuery] int take = 30, CancellationToken ct = default) =>
        (await Db.Notifications.AsNoTracking().Where(n => n.UserId == UserId).OrderByDescending(n => n.CreatedAt).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct))
        .Select(n => new NotificationDto(n.Id, n.Type.ToString(), n.Title, n.Body, n.CreatedAt, n.ReadAt is not null));

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        foreach (var n in await Db.Notifications.Where(n => n.UserId == UserId && n.ReadAt == null).ToListAsync(ct)) n.ReadAt = Clock.UtcNow;
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("preferences")]
    public async Task<NotificationPreferenceDto> Preferences(CancellationToken ct)
    {
        var p = await Db.NotificationPreferences.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId, ct) ?? new NotificationPreference();
        return new(p.Medicines, p.Appointments, p.Water, p.Weekly, p.Baby, p.QuietHoursStart, p.QuietHoursEnd);
    }

    [HttpPut("preferences")]
    public async Task<NotificationPreferenceDto> SavePreferences(NotificationPreferenceDto req, CancellationToken ct)
    {
        var p = await Db.NotificationPreferences.FirstOrDefaultAsync(x => x.UserId == UserId, ct);
        if (p is null) Db.NotificationPreferences.Add(p = new NotificationPreference { UserId = UserId });
        p.Medicines = req.Medicines; p.Appointments = req.Appointments; p.Water = req.Water; p.Weekly = req.Weekly; p.Baby = req.Baby;
        p.QuietHoursStart = req.QuietHoursStart; p.QuietHoursEnd = req.QuietHoursEnd;
        await Db.SaveChangesAsync(ct);
        return req;
    }
}
