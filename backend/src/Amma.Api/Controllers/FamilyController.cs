using System.Security.Cryptography;
using Amma.Api.Auth;
using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Care;
using Amma.Domain.Family;
using Amma.Domain.Pregnancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

/// <summary>The mother decides who can see what. Health records and reports are private by default.</summary>
[Route("api/v1/family")]
public class FamilyController(ContentQuery content, DoseService doses, AuditService audit) : ApiControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<FamilyMemberDto>> List(CancellationToken ct) =>
        (await Db.FamilyMembers.AsNoTracking().Where(f => f.UserId == UserId && f.Status != FamilyMemberStatus.Revoked).OrderBy(f => f.CreatedAt).ToListAsync(ct)).Select(f => f.ToDto());

    [HttpPost]
    public async Task<ActionResult<FamilyMemberDto>> Invite(InviteFamilyRequest req, CancellationToken ct)
    {
        string? phone = null;
        if (!string.IsNullOrWhiteSpace(req.Phone))
        {
            phone = Destination.Normalize(req.Phone);
            if (phone is null || Destination.IsEmail(phone)) return Problem422("Enter a valid Indian mobile number.");
        }
        var f = new FamilyMember
        {
            UserId = UserId, Name = req.Name.Trim(), Relation = req.Relation?.Trim() ?? "", Phone = phone, Role = req.Role,
            InviteCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)),
            ShareMedicines = req.Role == FamilyRole.Partner,
        };
        // If that person already has an Amma account, link them straight away.
        if (phone is not null)
        {
            var existing = await Db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Phone == phone, ct);
            if (existing is not null && existing.Id != UserId) { f.MemberUserId = existing.Id; f.Status = FamilyMemberStatus.Active; }
        }
        Db.FamilyMembers.Add(f);
        audit.Record(UserId, "family.invite", "FamilyMember", f.Id);
        await Db.SaveChangesAsync(ct);
        // SMS with the invite link is sent by the notification provider (to be configured).
        return f.ToDto();
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<FamilyMemberDto>> Update(Guid id, UpdateFamilyPermissionsRequest req, CancellationToken ct)
    {
        var f = await Db.FamilyMembers.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (f is null) return NotFound();
        f.ShareAppointments = req.ShareAppointments ?? f.ShareAppointments;
        f.ShareMedicines = req.ShareMedicines ?? f.ShareMedicines;
        f.ShareBabyUpdates = req.ShareBabyUpdates ?? f.ShareBabyUpdates;
        f.ShareHealth = req.ShareHealth ?? f.ShareHealth;
        f.ShareReports = req.ShareReports ?? f.ShareReports;
        f.Role = req.Role ?? f.Role;
        audit.Record(UserId, "family.permissions", "FamilyMember", f.Id);
        await Db.SaveChangesAsync(ct);
        return f.ToDto();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var f = await Db.FamilyMembers.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (f is null) return NotFound();
        f.Status = FamilyMemberStatus.Revoked;
        audit.Record(UserId, "family.revoke", "FamilyMember", f.Id);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ----- Partner / family view -----

    /// <summary>Journeys that other mothers have shared with the signed-in user.</summary>
    [HttpGet("shared-with-me")]
    public async Task<IEnumerable<SharedJourneyDto>> SharedWithMe(CancellationToken ct)
    {
        var links = await Db.FamilyMembers.AsNoTracking().Where(f => f.MemberUserId == UserId && f.Status == FamilyMemberStatus.Active).ToListAsync(ct);
        var result = new List<SharedJourneyDto>();
        foreach (var link in links) result.Add(await BuildShared(link, ct));
        return result;
    }

    private async Task<SharedJourneyDto> BuildShared(FamilyMember link, CancellationToken ct)
    {
        var mother = await Db.Users.AsNoTracking().FirstAsync(u => u.Id == link.UserId, ct);
        var today = Clock.Today(mother.TimeZone);
        var p = await Db.PregnancyProfiles.AsNoTracking().Where(x => x.UserId == mother.Id && x.Status == PregnancyStatus.Active).FirstOrDefaultAsync(ct);
        var ga = p is null ? (GestationalAge?)null : PregnancyCalculator.Calculate(p.Lmp, today);
        var week = link.ShareBabyUpdates && ga is not null ? (await content.WeekAsync(ga.Value.Weeks, ct))?.ToDto() : null;

        List<AppointmentDto>? appts = null;
        if (link.ShareAppointments)
            appts = (await Db.AppointmentsFor(mother.Id).Where(a => a.Status == AppointmentStatus.Scheduled && a.Date >= today).ToListAsync(ct))
                .OrderBy(a => a.Date).ThenBy(a => a.Time).Take(5).Select(a => a.ToDto()).ToList();
        var meds = link.ShareMedicines ? await doses.ForDateAsync(mother.Id, today, ct) : null;
        var bag = link.Role == FamilyRole.Partner ? await TrackingController.BagFor(Db, content, mother.Id, ct) : null;
        return new SharedJourneyDto(link.Id, mother.Name, link.Role, link.ShareBabyUpdates ? ga?.ToDto() : null, link.ShareBabyUpdates ? p?.Edd : null, week, appts, meds, bag);
    }
}

internal static class FamilyQueries
{
    public static IQueryable<Appointment> AppointmentsFor(this Amma.Infrastructure.Persistence.AmmaDbContext db, Guid motherId) =>
        db.Appointments.AsNoTracking().Where(a => a.UserId == motherId);
}
