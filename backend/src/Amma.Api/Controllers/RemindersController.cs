using Amma.Api.Contracts;
using Amma.Domain.Care;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

/// <summary>
/// Shared daily reminders (water, meals, snacks, juice, tablets). Everyone signed in to the account gets them,
/// and when one person marks an occurrence done, the other sees who did it.
/// </summary>
[Route("api/v1/reminders")]
public class RemindersController : ApiControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ReminderDto>> List(CancellationToken ct)
    {
        await EnsureDefaultsAsync(ct);
        return (await Db.Reminders.AsNoTracking().Where(r => r.UserId == UserId && r.IsActive).ToListAsync(ct))
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Times.Min()).Select(ToDto);
    }

    [HttpPost]
    public async Task<ActionResult<ReminderDto>> Create(SaveReminderRequest req, CancellationToken ct)
    {
        var r = new Reminder { UserId = UserId };
        Apply(r, req);
        r.SortOrder = SortFor(r.Kind);
        Db.Reminders.Add(r);
        await Db.SaveChangesAsync(ct);
        return ToDto(r);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReminderDto>> Update(Guid id, SaveReminderRequest req, CancellationToken ct)
    {
        var r = await Db.Reminders.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (r is null) return NotFound();
        Apply(r, req);
        r.SortOrder = SortFor(r.Kind);
        await Db.SaveChangesAsync(ct);
        return ToDto(r);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var r = await Db.Reminders.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (r is null) return NotFound();
        r.IsActive = false; // history is kept
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>All occurrences for a day with who completed them.</summary>
    [HttpGet("day")]
    public async Task<DayDto> Day([FromQuery] DateOnly? date, CancellationToken ct)
    {
        await EnsureDefaultsAsync(ct);
        var d = date ?? await TodayAsync(ct);
        var reminders = await Db.Reminders.AsNoTracking().Where(r => r.UserId == UserId && r.IsActive).ToListAsync(ct);
        var logs = await Db.ReminderLogs.AsNoTracking().Where(l => l.UserId == UserId && l.Date == d).ToListAsync(ct);
        var items = reminders
            .SelectMany(r => r.Times.Select(t =>
            {
                var log = logs.FirstOrDefault(l => l.ReminderId == r.Id && l.Time == t);
                return new OccurrenceDto(r.Id, r.Kind, r.Title, r.Note, t, log?.Status, log?.ByName, log?.UpdatedAt);
            }))
            .OrderBy(o => o.Time).ThenBy(o => o.Kind).ToList();
        return new DayDto(d, items, items.Count(i => i.Status == ReminderStatus.Done), items.Count);
    }

    /// <summary>Mark an occurrence done or skipped (status null clears it). Records who did it.</summary>
    [HttpPost("{id:guid}/log")]
    public async Task<ActionResult<OccurrenceDto>> Log(Guid id, LogReminderRequest req, CancellationToken ct)
    {
        var r = await Db.Reminders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (r is null) return NotFound();
        if (!r.Times.Contains(req.Time)) return Problem422("That time is not part of this reminder.");
        var today = await TodayAsync(ct);
        var date = req.Date ?? today;
        if (date > today) return Problem422("You can't complete a future reminder.");

        var log = await Db.ReminderLogs.FirstOrDefaultAsync(l => l.ReminderId == id && l.Date == date && l.Time == req.Time, ct);
        var name = await PersonNameAsync(ct);
        if (req.Status is null) { if (log is not null) Db.ReminderLogs.Remove(log); }
        else
        {
            if (log is null) Db.ReminderLogs.Add(log = new ReminderLog { UserId = UserId, ReminderId = id, Date = date, Time = req.Time });
            log.Status = req.Status.Value; log.ByLoginId = LoginId; log.ByName = name;
        }
        await Db.SaveChangesAsync(ct);
        return new OccurrenceDto(r.Id, r.Kind, r.Title, r.Note, req.Time, req.Status, req.Status is null ? null : name, req.Status is null ? null : log?.UpdatedAt);
    }

    /// <summary>History: how many reminders were done each day.</summary>
    [HttpGet("history")]
    public async Task<IEnumerable<object>> History([FromQuery] int days = 14, CancellationToken ct = default)
    {
        var to = await TodayAsync(ct);
        var from = to.AddDays(-Math.Clamp(days, 1, 90) + 1);
        var logs = await Db.ReminderLogs.AsNoTracking().Where(l => l.UserId == UserId && l.Date >= from && l.Date <= to && l.Status == ReminderStatus.Done).ToListAsync(ct);
        return Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i))
            .Select(d => new { date = d, done = logs.Count(l => l.Date == d) }).Reverse();
    }

    /// <summary>Creates a starter schedule the first time (and converts any tablets added earlier).</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> SeedLocks = new();

    private async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        if (await Db.Reminders.AnyAsync(r => r.UserId == UserId, ct)) return;
        // Both phones (and several screens) may ask at the same moment: create the starter list only once.
        var gate = SeedLocks.GetOrAdd(UserId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try { await SeedAsync(ct); } finally { gate.Release(); }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        if (await Db.Reminders.AnyAsync(r => r.UserId == UserId, ct)) return;
        var list = new List<Reminder>
        {
            New(ReminderKind.Water, "Drink a glass of water", null, [new(7, 30), new(9, 30), new(11, 30), new(13, 30), new(15, 30), new(17, 30), new(19, 30), new(21, 0)]),
            New(ReminderKind.Meal, "Breakfast", null, [new(8, 30)]),
            New(ReminderKind.Snack, "Morning snack", "Fruit, nuts or sundal", [new(11, 0)]),
            New(ReminderKind.Meal, "Lunch", null, [new(13, 30)]),
            New(ReminderKind.Juice, "Juice / coconut water", null, [new(16, 0)]),
            New(ReminderKind.Snack, "Evening snack", null, [new(17, 30)]),
            New(ReminderKind.Meal, "Dinner", null, [new(20, 30)]),
        };
        foreach (var m in await Db.Medications.AsNoTracking().Where(m => m.UserId == UserId && m.IsActive).ToListAsync(ct))
            list.Add(New(ReminderKind.Tablet, m.Name, string.Join(" · ", new[] { m.Dosage, m.Instructions }.Where(x => !string.IsNullOrWhiteSpace(x))), m.ReminderTimes));
        Db.Reminders.AddRange(list);
        await Db.SaveChangesAsync(ct);

        Reminder New(ReminderKind k, string title, string? note, List<TimeOnly> times) =>
            new() { UserId = UserId, Kind = k, Title = title, Note = note, Times = times, SortOrder = SortFor(k) };
    }

    private static int SortFor(ReminderKind k) => k switch
    {
        ReminderKind.Tablet => 0, ReminderKind.Water => 1, ReminderKind.Meal => 2, ReminderKind.Snack => 3, ReminderKind.Juice => 4, _ => 5,
    };

    private static void Apply(Reminder r, SaveReminderRequest req)
    {
        r.Kind = req.Kind; r.Title = req.Title.Trim(); r.Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
        r.Times = req.Times.Distinct().Order().ToList(); r.IsActive = true;
    }

    private static ReminderDto ToDto(Reminder r) => new(r.Id, r.Kind, r.Title, r.Note, r.Times.Order().ToList(), r.IsActive, r.SortOrder);
}
