using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Care;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

[Route("api/v1/appointments")]
public class AppointmentsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<AppointmentDto>> List([FromQuery] AppointmentStatus? status, CancellationToken ct)
    {
        var q = Db.Appointments.AsNoTracking().Where(a => a.UserId == UserId);
        if (status is not null) q = q.Where(a => a.Status == status);
        var list = await q.ToListAsync(ct);
        var ordered = status == AppointmentStatus.Completed
            ? list.OrderByDescending(a => a.Date).ThenByDescending(a => a.Time)
            : list.OrderBy(a => a.Date).ThenBy(a => a.Time);
        return ordered.Select(a => a.ToDto());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AppointmentDto>> Get(Guid id, CancellationToken ct)
    {
        var a = await Db.Appointments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        return a is null ? NotFound() : a.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create(SaveAppointmentRequest req, CancellationToken ct)
    {
        var a = new Appointment { UserId = UserId };
        Apply(a, req);
        Db.Appointments.Add(a);
        await Db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = a.Id }, a.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AppointmentDto>> Update(Guid id, SaveAppointmentRequest req, CancellationToken ct)
    {
        var a = await Db.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (a is null) return NotFound();
        Apply(a, req);
        await Db.SaveChangesAsync(ct);
        return a.ToDto();
    }

    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<AppointmentDto>> SetStatus(Guid id, [FromQuery] AppointmentStatus status, CancellationToken ct)
    {
        var a = await Db.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (a is null) return NotFound();
        a.Status = status;
        await Db.SaveChangesAsync(ct);
        return a.ToDto();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var a = await Db.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (a is null) return NotFound();
        Db.Appointments.Remove(a);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static void Apply(Appointment a, SaveAppointmentRequest r)
    {
        a.Title = r.Title.Trim(); a.DoctorName = r.DoctorName?.Trim(); a.Location = r.Location?.Trim();
        a.Date = r.Date; a.Time = r.Time; a.Purpose = r.Purpose?.Trim(); a.Notes = r.Notes?.Trim();
        a.Category = r.Category; a.ReminderMinutesBefore = r.ReminderMinutesBefore;
    }
}
