using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Content;
using Amma.Domain.Tracking;
using Amma.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

/// <summary>Daily trackers: water, checklist, mood/sleep/symptoms journal and the hospital bag.</summary>
[Route("api/v1/tracking")]
public class TrackingController(ContentQuery content) : ApiControllerBase
{
    // ----- Water -----
    [HttpGet("water")]
    public async Task<WaterDto> Water([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var d = date ?? await TodayAsync(ct);
        var w = await Db.WaterLogs.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId && x.Date == d, ct);
        return new WaterDto(d, w?.Glasses ?? 0, await GoalAsync(ct));
    }

    [HttpPut("water")]
    public async Task<ActionResult<WaterDto>> SetWater(SetWaterRequest req, CancellationToken ct)
    {
        var today = await TodayAsync(ct);
        var d = req.Date ?? today;
        if (d > today) return Problem422("You can't log water for a future date.");
        var w = await Db.WaterLogs.FirstOrDefaultAsync(x => x.UserId == UserId && x.Date == d, ct);
        if (w is null) Db.WaterLogs.Add(w = new WaterLog { UserId = UserId, Date = d });
        w.Glasses = req.Glasses;
        await Db.SaveChangesAsync(ct);
        return new WaterDto(d, w.Glasses, await GoalAsync(ct));
    }

    private async Task<int> GoalAsync(CancellationToken ct) => (await ActivePregnancyAsync(ct))?.WaterGoalGlasses ?? 10;

    // ----- Daily checklist -----
    [HttpGet("checklist")]
    public async Task<ChecklistDto> Checklist([FromQuery] DateOnly? date, CancellationToken ct) =>
        await ChecklistFor(Db, content, UserId, date ?? await TodayAsync(ct), ct);

    [HttpPut("checklist/{key}")]
    public async Task<ActionResult<ChecklistDto>> SetChecklist(string key, SetChecklistRequest req, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var d = date ?? await TodayAsync(ct);
        var items = await content.SnippetsAsync(SnippetKind.DailyChecklist, ct);
        if (items.All(i => i.Key != key)) return NotFound();
        var log = await DailyAsync(d, ct);
        log.ChecklistDone = req.Done ? log.ChecklistDone.Append(key).Distinct().ToList() : log.ChecklistDone.Where(k => k != key).ToList();
        await Db.SaveChangesAsync(ct);
        return await ChecklistFor(Db, content, UserId, d, ct);
    }

    internal static async Task<ChecklistDto> ChecklistFor(AmmaDbContext db, ContentQuery content, Guid userId, DateOnly date, CancellationToken ct)
    {
        var items = await content.SnippetsAsync(SnippetKind.DailyChecklist, ct);
        var log = await db.DailyLogs.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.Date == date, ct);
        var done = log?.ChecklistDone ?? [];
        return new ChecklistDto(date, items.Select(i => new ChecklistItemDto(i.Key, i.Title, done.Contains(i.Key))).ToList());
    }

    // ----- Mood, sleep, symptoms, journal -----
    [HttpGet("daily")]
    public async Task<IEnumerable<DailyLogDto>> DailyHistory([FromQuery] int days = 14, CancellationToken ct = default)
    {
        var from = (await TodayAsync(ct)).AddDays(-Math.Clamp(days, 1, 120));
        return (await Db.DailyLogs.AsNoTracking().Where(x => x.UserId == UserId && x.Date > from).OrderByDescending(x => x.Date).ToListAsync(ct)).Select(x => x.ToDto(x.Date));
    }

    [HttpPut("daily/{date}")]
    public async Task<ActionResult<DailyLogDto>> SaveDaily(DateOnly date, SaveDailyLogRequest req, CancellationToken ct)
    {
        if (date > await TodayAsync(ct)) return Problem422("You can't log a future date.");
        var log = await DailyAsync(date, ct);
        if (req.Mood is not null) log.Mood = req.Mood;
        if (req.SleepHours is not null) log.SleepHours = req.SleepHours;
        if (req.Symptoms is not null) log.Symptoms = req.Symptoms.Select(s => s.Trim()).Where(s => s.Length is > 0 and <= 60).Distinct().Take(30).ToList();
        if (req.Journal is not null) log.Journal = req.Journal;
        await Db.SaveChangesAsync(ct);
        return log.ToDto(date);
    }

    private async Task<DailyLog> DailyAsync(DateOnly d, CancellationToken ct)
    {
        var log = await Db.DailyLogs.FirstOrDefaultAsync(x => x.UserId == UserId && x.Date == d, ct);
        if (log is null) Db.DailyLogs.Add(log = new DailyLog { UserId = UserId, Date = d });
        return log;
    }

    // ----- Hospital bag -----
    [HttpGet("hospital-bag")]
    public async Task<HospitalBagDto> Bag(CancellationToken ct) => await BagFor(Db, content, UserId, ct);

    [HttpPut("hospital-bag/{key}")]
    public async Task<ActionResult<HospitalBagDto>> SetBag(string key, SetBagItemRequest req, CancellationToken ct)
    {
        var items = await content.SnippetsAsync(SnippetKind.HospitalBag, ct);
        if (items.All(i => i.Key != key)) return NotFound();
        var c = await Db.HospitalBagChecks.FirstOrDefaultAsync(x => x.UserId == UserId && x.ItemKey == key, ct);
        if (c is null) Db.HospitalBagChecks.Add(c = new HospitalBagCheck { UserId = UserId, ItemKey = key });
        c.Packed = req.Packed;
        await Db.SaveChangesAsync(ct);
        return await BagFor(Db, content, UserId, ct);
    }

    internal static async Task<HospitalBagDto> BagFor(AmmaDbContext db, ContentQuery content, Guid userId, CancellationToken ct)
    {
        var items = await content.SnippetsAsync(SnippetKind.HospitalBag, ct);
        var packed = await db.HospitalBagChecks.AsNoTracking().Where(x => x.UserId == userId && x.Packed).Select(x => x.ItemKey).ToListAsync(ct);
        var dto = items.Select(i => new BagItemDto(i.Key, i.Group ?? "Other", i.Title, packed.Contains(i.Key))).ToList();
        var pct = dto.Count == 0 ? 0 : (int)Math.Round(dto.Count(x => x.Packed) * 100.0 / dto.Count);
        return new HospitalBagDto(pct, dto);
    }
}
