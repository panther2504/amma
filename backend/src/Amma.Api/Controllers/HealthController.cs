using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Pregnancy;
using Amma.Domain.Tracking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

/// <summary>
/// Mother's health tracker. Records readings for the mother and her doctor; it shows clinician-approved
/// reference guidance but never diagnoses.
/// </summary>
[Route("api/v1/health")]
public class HealthController : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<HealthSummaryDto> Summary(CancellationToken ct)
    {
        var today = await TodayAsync(ct);
        var weights = await Db.WeightLogs.AsNoTracking().Where(x => x.UserId == UserId).OrderBy(x => x.Date).ToListAsync(ct);
        var bp = await Db.BloodPressureLogs.AsNoTracking().Where(x => x.UserId == UserId).OrderByDescending(x => x.MeasuredAt).Take(30).ToListAsync(ct);
        var sugar = await Db.BloodSugarLogs.AsNoTracking().Where(x => x.UserId == UserId).OrderByDescending(x => x.MeasuredAt).Take(30).ToListAsync(ct);
        var daily = await Db.DailyLogs.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId && x.Date == today, ct);
        var water = await Db.WaterLogs.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId && x.Date == today, ct);
        return new HealthSummaryDto(weights.LastOrDefault()?.ToDto(), bp.FirstOrDefault()?.ToDto(), sugar.FirstOrDefault()?.ToDto(), daily.ToDto(today), water?.Glasses ?? 0,
            weights.Select(w => w.ToDto()).ToList(), bp.Select(b => b.ToDto()).ToList(), sugar.Select(s => s.ToDto()).ToList());
    }

    [HttpPost("weights")]
    public async Task<ActionResult<SavedReading<WeightDto>>> AddWeight(AddWeightRequest req, CancellationToken ct)
    {
        var today = await TodayAsync(ct);
        var date = req.Date ?? today;
        if (date > today) return Problem422("You can't log a future date.");
        var p = await ActivePregnancyAsync(ct);
        var week = p is null ? 0 : PregnancyCalculator.Calculate(p.Lmp, date).Weeks;
        // One weight per day: a new reading on the same day replaces it.
        var existing = await Db.WeightLogs.FirstOrDefaultAsync(x => x.UserId == UserId && x.Date == date, ct);
        var w = existing ?? new WeightLog { UserId = UserId };
        w.Date = date; w.GestationalWeek = week; w.Kg = req.Kg;
        if (existing is null) Db.WeightLogs.Add(w);
        await Db.SaveChangesAsync(ct);
        return new SavedReading<WeightDto>(w.ToDto(), null);
    }

    [HttpDelete("weights/{id:guid}")]
    public Task<IActionResult> DeleteWeight(Guid id, CancellationToken ct) => DeleteOwned<WeightLog>(id, ct);

    [HttpPost("blood-pressure")]
    public async Task<ActionResult<SavedReading<BloodPressureDto>>> AddBp(AddBloodPressureRequest req, CancellationToken ct)
    {
        if (req.Diastolic >= req.Systolic) return Problem422("The first (systolic) number is usually higher than the second (diastolic). Please check the reading.");
        var b = new BloodPressureLog { UserId = UserId, MeasuredAt = req.MeasuredAt ?? Clock.UtcNow, Systolic = req.Systolic, Diastolic = req.Diastolic, Pulse = req.Pulse };
        Db.BloodPressureLogs.Add(b);
        await Db.SaveChangesAsync(ct);
        return new SavedReading<BloodPressureDto>(b.ToDto(), HealthAdvisor.ForBloodPressure(b.Systolic, b.Diastolic));
    }

    [HttpDelete("blood-pressure/{id:guid}")]
    public Task<IActionResult> DeleteBp(Guid id, CancellationToken ct) => DeleteOwned<BloodPressureLog>(id, ct);

    [HttpPost("blood-sugar")]
    public async Task<ActionResult<SavedReading<BloodSugarDto>>> AddSugar(AddBloodSugarRequest req, CancellationToken ct)
    {
        var s = new BloodSugarLog { UserId = UserId, MeasuredAt = req.MeasuredAt ?? Clock.UtcNow, MgDl = req.MgDl, Context = req.Context };
        Db.BloodSugarLogs.Add(s);
        await Db.SaveChangesAsync(ct);
        return new SavedReading<BloodSugarDto>(s.ToDto(), HealthAdvisor.ForBloodSugar(s.MgDl, s.Context));
    }

    [HttpDelete("blood-sugar/{id:guid}")]
    public Task<IActionResult> DeleteSugar(Guid id, CancellationToken ct) => DeleteOwned<BloodSugarLog>(id, ct);

    // ----- Baby movements -----
    [HttpGet("kick-sessions")]
    public async Task<IEnumerable<KickSessionDto>> Kicks([FromQuery] int take = 20, CancellationToken ct = default) =>
        (await Db.KickSessions.AsNoTracking().Where(x => x.UserId == UserId).OrderByDescending(x => x.StartedAt).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct)).Select(k => k.ToDto());

    [HttpPost("kick-sessions")]
    public async Task<ActionResult<KickSessionDto>> AddKick(SaveKickSessionRequest req, CancellationToken ct)
    {
        if (req.EndedAt < req.StartedAt) return Problem422("End time must be after start time.");
        if ((req.EndedAt - req.StartedAt).TotalHours > 12) return Problem422("A session can't be longer than 12 hours.");
        var k = new KickSession { UserId = UserId, StartedAt = req.StartedAt, EndedAt = req.EndedAt, Movements = req.Movements, Notes = req.Notes };
        Db.KickSessions.Add(k);
        await Db.SaveChangesAsync(ct);
        return k.ToDto();
    }

    [HttpDelete("kick-sessions/{id:guid}")]
    public Task<IActionResult> DeleteKick(Guid id, CancellationToken ct) => DeleteOwned<KickSession>(id, ct);

    private async Task<IActionResult> DeleteOwned<T>(Guid id, CancellationToken ct) where T : Domain.Common.Entity, Domain.Common.IUserOwned
    {
        var e = await Db.Set<T>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (e is null) return NotFound();
        Db.Set<T>().Remove(e);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }
}
