using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Care;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

/// <summary>
/// Reminders for medicines prescribed by the user's doctor. Amma never suggests, changes or stops medicines;
/// it only stores and reminds what the user enters from their prescription.
/// </summary>
[Route("api/v1/medications")]
public class MedicationsController(DoseService doses, AuditService audit) : ApiControllerBase
{
    private static readonly string[] Colors = ["sage", "red", "sky", "lav", "amber", "peach"];

    [HttpGet]
    public async Task<IEnumerable<MedicationDto>> List([FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        (await Db.Medications.AsNoTracking().Where(m => m.UserId == UserId && (includeInactive || m.IsActive)).OrderBy(m => m.CreatedAt).ToListAsync(ct)).Select(m => m.ToDto());

    [HttpGet("doses")]
    public async Task<IEnumerable<DoseDto>> Doses([FromQuery] DateOnly? date, CancellationToken ct) =>
        await doses.ForDateAsync(UserId, date ?? await TodayAsync(ct), ct);

    [HttpGet("history")]
    public async Task<IEnumerable<MedicationHistoryDto>> History([FromQuery] int days = 14, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 90);
        var today = await TodayAsync(ct);
        var result = new List<MedicationHistoryDto>();
        for (var i = 0; i < days; i++)
        {
            var d = today.AddDays(-i);
            var list = await doses.ForDateAsync(UserId, d, ct);
            result.Add(new(d, list.Count, list.Count(x => x.Status == MedicationLogStatus.Taken), list.Count(x => x.Status == MedicationLogStatus.Skipped)));
        }
        return result;
    }

    [HttpPost]
    public async Task<ActionResult<MedicationDto>> Create(SaveMedicationRequest req, CancellationToken ct)
    {
        var count = await Db.Medications.CountAsync(m => m.UserId == UserId, ct);
        var m = new Medication { UserId = UserId, ColorKey = Colors[count % Colors.Length] };
        var err = await Apply(m, req, ct);
        if (err is not null) return err;
        Db.Medications.Add(m);
        audit.Record(UserId, "medication.create", "Medication", m.Id);
        await Db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), m.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MedicationDto>> Update(Guid id, SaveMedicationRequest req, CancellationToken ct)
    {
        var m = await Db.Medications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (m is null) return NotFound();
        var err = await Apply(m, req, ct);
        if (err is not null) return err;
        audit.Record(UserId, "medication.update", "Medication", m.Id);
        await Db.SaveChangesAsync(ct);
        return m.ToDto();
    }

    /// <summary>Stops reminders for a medicine (history is kept). Only do this as advised by your doctor.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var m = await Db.Medications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (m is null) return NotFound();
        m.IsActive = false;
        audit.Record(UserId, "medication.deactivate", "Medication", m.Id);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Record a dose as taken or skipped. Send status = null to clear.</summary>
    [HttpPost("{id:guid}/log")]
    public async Task<ActionResult<DoseDto>> Log(Guid id, LogDoseRequest req, CancellationToken ct)
    {
        var m = await Db.Medications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (m is null) return NotFound();
        if (!m.ReminderTimes.Contains(req.Time)) return Problem422("That time is not part of this medicine's schedule.");
        var date = req.Date ?? await TodayAsync(ct);
        if (date > await TodayAsync(ct)) return Problem422("You can't log a future dose.");

        var log = await Db.MedicationLogs.FirstOrDefaultAsync(l => l.MedicationId == id && l.Date == date && l.ScheduledTime == req.Time, ct);
        if (req.Status is null) { if (log is not null) Db.MedicationLogs.Remove(log); }
        else if (log is null) Db.MedicationLogs.Add(new MedicationLog { UserId = UserId, MedicationId = id, Date = date, ScheduledTime = req.Time, Status = req.Status.Value });
        else log.Status = req.Status.Value;
        await Db.SaveChangesAsync(ct);
        return new DoseDto(m.Id, m.Name, m.Dosage, m.Instructions, m.ColorKey, req.Time, Mapping.SlotFor(req.Time), req.Status);
    }

    private async Task<ActionResult?> Apply(Medication m, SaveMedicationRequest req, CancellationToken ct)
    {
        var today = await TodayAsync(ct);
        var start = req.StartDate ?? today;
        if (req.EndDate is not null && req.EndDate < start) return Problem422("End date must be after the start date.");
        m.Name = req.Name.Trim(); m.Dosage = req.Dosage.Trim(); m.Instructions = req.Instructions?.Trim();
        m.Category = string.IsNullOrWhiteSpace(req.Category) ? "Other" : req.Category;
        m.ReminderTimes = req.ReminderTimes.Distinct().Order().ToList();
        m.StartDate = start; m.EndDate = req.EndDate; m.RefillOn = req.RefillOn;
        if (req.ColorKey is not null && Colors.Contains(req.ColorKey)) m.ColorKey = req.ColorKey;
        m.IsActive = true;
        return null;
    }
}
