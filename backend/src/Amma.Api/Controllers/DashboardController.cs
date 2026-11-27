using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Care;
using Amma.Domain.Content;
using Amma.Domain.Pregnancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

[Route("api/v1/dashboard")]
public class DashboardController(ContentQuery content, DoseService doses) : ApiControllerBase
{
    [HttpGet]
    public async Task<DashboardDto> Get(CancellationToken ct)
    {
        var me = await MeAsync(ct);
        var today = Clock.Today(me.TimeZone);
        var p = await ActivePregnancyAsync(ct);
        var ga = p is null ? (GestationalAge?)null : PregnancyCalculator.Calculate(p.Lmp, today);

        var week = ga is null ? null : await content.WeekAsync(ga.Value.Weeks, ct);
        var guide = ga is null ? null : await content.GuideAsync(ga.Value.Weeks, ct);
        var tip = guide?.Tips.Count > 0 ? guide.Tips[today.DayNumber % guide.Tips.Count] : null;

        var water = await Db.WaterLogs.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == UserId && w.Date == today, ct);
        var next = await Db.Appointments.AsNoTracking()
            .Where(a => a.UserId == UserId && a.Status == AppointmentStatus.Scheduled && a.Date >= today)
            .OrderBy(a => a.Date).ThenBy(a => a.Time).FirstOrDefaultAsync(ct);
        var diet = ContentQuery.DietKey(p?.Diet ?? DietPreference.Vegetarian);
        var meals = await content.Visible<MealPlanItem>().Where(m => m.Diet == diet).OrderBy(m => m.Order).ToListAsync(ct);
        var unread = await Db.Notifications.CountAsync(n => n.UserId == UserId && n.ReadAt == null, ct);

        return new DashboardDto(
            me.ToDto(p is not null), p?.ToDto(today), week?.ToDto(), tip,
            await doses.ForDateAsync(UserId, today, ct),
            new WaterDto(today, water?.Glasses ?? 0, p?.WaterGoalGlasses ?? 10),
            next?.ToDto(), meals.Select(m => m.ToDto()).ToList(),
            await TrackingController.ChecklistFor(Db, content, UserId, today, ct), unread);
    }
}
