using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Common;
using Amma.Domain.Family;
using Amma.Domain.Notifications;
using Amma.Domain.Pregnancy;
using Amma.Domain.Users;
using Amma.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

[Route("api/v1/me")]
public class MeController(AuditService audit, IFileStorage storage) : ApiControllerBase
{
    [HttpGet]
    public async Task<UserDto> Get(CancellationToken ct)
    {
        var me = await MeAsync(ct);
        return me.ToDto(await ActivePregnancyAsync(ct) is not null);
    }

    [HttpPut]
    public async Task<UserDto> Update(UpdateMeRequest req, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        if (!string.IsNullOrWhiteSpace(req.Name)) me.Name = req.Name.Trim();
        if (req.Language is not null) me.Language = req.Language;
        if (req.TimeZone is not null) me.TimeZone = UserClock.Find(req.TimeZone).Id;
        await Db.SaveChangesAsync(ct);
        return me.ToDto(await ActivePregnancyAsync(ct) is not null);
    }

    /// <summary>The person on this phone (PIN mode): their name and number.</summary>
    [HttpGet("person")]
    public async Task<PersonDto> Person(CancellationToken ct)
    {
        var login = LoginId is { } lid ? await Db.LoginIdentities.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lid, ct) : null;
        return new PersonDto(await PersonNameAsync(ct), login?.Destination, login is not null);
    }

    [HttpPut("person")]
    public async Task<ActionResult<PersonDto>> SetPerson(SetPersonRequest req, CancellationToken ct)
    {
        var login = LoginId is { } lid ? await Db.LoginIdentities.FirstOrDefaultAsync(l => l.Id == lid && l.UserId == UserId, ct) : null;
        if (login is null) return Problem422("Only available when signed in with a PIN.");
        login.DisplayName = req.Name.Trim();
        await Db.SaveChangesAsync(ct);
        return new PersonDto(login.DisplayName, login.Destination, true);
    }

    /// <summary>Changes the signed-in person's own PIN (PIN sign-in mode).</summary>
    [HttpPut("pin")]
    public async Task<IActionResult> ChangePin(ChangePinRequest req, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        var lid = Guid.TryParse(User.FindFirst(Amma.Api.Auth.TokenService.LoginClaim)?.Value, out var id) ? id : (Guid?)null;
        var login = lid is not null
            ? await Db.LoginIdentities.FirstOrDefaultAsync(l => l.Id == lid && l.UserId == UserId, ct)
            : await Db.LoginIdentities.FirstOrDefaultAsync(l => l.UserId == UserId && l.Destination == me.Phone, ct);
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
        if (login?.PinHash is null || hasher.VerifyHashedPassword(me, login.PinHash, req.CurrentPin) == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            return Problem(detail: "Your current PIN is not correct.", statusCode: 400);
        login.PinHash = hasher.HashPassword(me, req.NewPin);
        audit.Record(UserId, "auth.pin.change", "LoginIdentity", login.Id);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ----- Consent management -----
    [HttpGet("consents")]
    public async Task<IEnumerable<ConsentDto>> Consents(CancellationToken ct)
    {
        var rows = await Db.UserConsents.AsNoTracking().Where(c => c.UserId == UserId).ToListAsync(ct);
        return Enum.GetValues<ConsentType>().Select(t =>
        {
            var c = rows.Where(r => r.Type == t).OrderByDescending(r => r.UpdatedAt).FirstOrDefault();
            return new ConsentDto(t, c?.Granted ?? false, c?.PolicyVersion ?? "2026-01", c?.UpdatedAt ?? DateTimeOffset.MinValue);
        });
    }

    [HttpPut("consents")]
    public async Task<IActionResult> SetConsent(UpdateConsentRequest req, CancellationToken ct)
    {
        // Consent changes are append-only so there is a full history.
        Db.UserConsents.Add(new UserConsent { UserId = UserId, Type = req.Type, Granted = req.Granted });
        audit.Record(UserId, req.Granted ? "consent.grant" : "consent.revoke", "Consent", req.Type);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ----- Emergency contacts -----
    [HttpGet("emergency-contacts")]
    public async Task<IEnumerable<EmergencyContactDto>> Contacts(CancellationToken ct) =>
        (await Db.EmergencyContacts.AsNoTracking().Where(c => c.UserId == UserId).OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Name).ToListAsync(ct)).Select(c => c.ToDto());

    [HttpPost("emergency-contacts")]
    public async Task<EmergencyContactDto> AddContact(SaveEmergencyContactRequest req, CancellationToken ct)
    {
        var c = new EmergencyContact { UserId = UserId };
        await ApplyContact(c, req, ct);
        Db.EmergencyContacts.Add(c);
        await Db.SaveChangesAsync(ct);
        return c.ToDto();
    }

    [HttpPut("emergency-contacts/{id:guid}")]
    public async Task<ActionResult<EmergencyContactDto>> UpdateContact(Guid id, SaveEmergencyContactRequest req, CancellationToken ct)
    {
        var c = await Db.EmergencyContacts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (c is null) return NotFound();
        await ApplyContact(c, req, ct);
        await Db.SaveChangesAsync(ct);
        return c.ToDto();
    }

    [HttpDelete("emergency-contacts/{id:guid}")]
    public async Task<IActionResult> DeleteContact(Guid id, CancellationToken ct)
    {
        var c = await Db.EmergencyContacts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (c is null) return NotFound();
        Db.EmergencyContacts.Remove(c);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task ApplyContact(EmergencyContact c, SaveEmergencyContactRequest req, CancellationToken ct)
    {
        c.Name = req.Name.Trim(); c.Relation = req.Relation?.Trim() ?? ""; c.Phone = req.Phone.Trim();
        c.IsPrimary = req.IsPrimary; c.NotifyInEmergency = req.NotifyInEmergency;
        if (req.IsPrimary)
            foreach (var other in await Db.EmergencyContacts.Where(x => x.UserId == UserId && x.Id != c.Id && x.IsPrimary).ToListAsync(ct))
                other.IsPrimary = false;
    }

    /// <summary>Notify trusted contacts. Location is included only when the mother explicitly chooses to share it.</summary>
    [HttpPost("emergency-alert")]
    public async Task<EmergencyAlertResponse> Alert(EmergencyAlertRequest req, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        var location = req.ShareLocation && req.Latitude is not null && req.Longitude is not null
            ? $" Location: https://maps.google.com/?q={req.Latitude:0.00000},{req.Longitude:0.00000}" : "";
        var family = await Db.FamilyMembers.Where(f => f.UserId == UserId && f.Status == FamilyMemberStatus.Active && f.MemberUserId != null).ToListAsync(ct);
        var stamp = Clock.UtcNow.ToUnixTimeSeconds();
        foreach (var f in family)
            Db.Notifications.Add(new Notification { UserId = f.MemberUserId!.Value, Type = NotificationType.Family, Title = $"🚨 {me.Name} needs help", Body = $"{me.Name} used the emergency button in Amma. Please call her now.{location}", DedupKey = $"sos:{me.Id}:{stamp}" });
        var contacts = await Db.EmergencyContacts.AsNoTracking().Where(c => c.UserId == UserId && c.NotifyInEmergency).ToListAsync(ct);
        // SMS delivery to contacts is handled by the notification provider (to be configured).
        audit.Record(UserId, "emergency.alert", null, req.ShareLocation ? "with-location" : "no-location");
        await Db.SaveChangesAsync(ct);
        var names = family.Select(f => f.Name).Concat(contacts.Select(c => c.Name)).Distinct().ToList();
        return new EmergencyAlertResponse(names.Count, names);
    }

    // ----- Data rights -----
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var id = UserId;
        var data = new
        {
            exportedAt = Clock.UtcNow,
            user = await Db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => new { u.Name, u.Email, u.Phone, u.Language, u.TimeZone, u.CreatedAt }).FirstAsync(ct),
            pregnancies = await Db.PregnancyProfiles.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
            medications = await Db.Medications.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
            medicationLogs = await Db.MedicationLogs.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
            appointments = await Db.Appointments.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
            water = await Db.WaterLogs.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
            weights = await Db.WeightLogs.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
            bloodPressure = await Db.BloodPressureLogs.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
            bloodSugar = await Db.BloodSugarLogs.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
            dailyLogs = await Db.DailyLogs.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
            kickSessions = await Db.KickSessions.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
            family = await Db.FamilyMembers.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
            emergencyContacts = await Db.EmergencyContacts.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
            documents = await Db.MedicalDocuments.AsNoTracking().Where(x => x.UserId == id).Select(d => new { d.Title, d.Category, d.DocumentDate, d.FileName, d.SizeBytes }).ToListAsync(ct),
            assistant = await Db.AssistantMessages.AsNoTracking().Where(x => x.UserId == id).Select(m => new { m.Role, m.Content, m.CreatedAt }).ToListAsync(ct),
            consents = await Db.UserConsents.AsNoTracking().Where(x => x.UserId == id).ToListAsync(ct),
        };
        audit.Record(UserId, "data.export");
        await Db.SaveChangesAsync(ct);
        Response.Headers.ContentDisposition = "attachment; filename=amma-my-data.json";
        return Ok(data);
    }

    /// <summary>Permanently deletes the account and all personal health data.</summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var id = UserId;
        foreach (var d in await Db.MedicalDocuments.Where(x => x.UserId == id).ToListAsync(ct))
            await storage.DeleteAsync(d.StorageKey, ct);

        await RemoveAll<RefreshToken>(id, ct); await RemoveAll<LoginIdentity>(id, ct); await RemoveAll<UserConsent>(id, ct); await RemoveAll<EmergencyContact>(id, ct);
        await RemoveAll<PregnancyProfile>(id, ct); await RemoveAll<Domain.Care.Medication>(id, ct); await RemoveAll<Domain.Care.MedicationLog>(id, ct);
        await RemoveAll<Domain.Care.Appointment>(id, ct); await RemoveAll<Domain.Tracking.WaterLog>(id, ct); await RemoveAll<Domain.Tracking.WeightLog>(id, ct);
        await RemoveAll<Domain.Tracking.BloodPressureLog>(id, ct); await RemoveAll<Domain.Tracking.BloodSugarLog>(id, ct); await RemoveAll<Domain.Tracking.DailyLog>(id, ct);
        await RemoveAll<Domain.Tracking.KickSession>(id, ct); await RemoveAll<Domain.Tracking.HospitalBagCheck>(id, ct); await RemoveAll<FamilyMember>(id, ct);
        await RemoveAll<Domain.Documents.MedicalDocument>(id, ct); await RemoveAll<Notification>(id, ct); await RemoveAll<NotificationPreference>(id, ct);
        await RemoveAll<Domain.Assistant.AssistantMessage>(id, ct);
        foreach (var mp in await Db.MemoryPhotos.Where(x => x.UserId == id && !x.PhotoKey.StartsWith("gdrive:")).ToListAsync(ct)) await storage.DeleteAsync(mp.PhotoKey, ct);
        await RemoveAll<Domain.Documents.DriveConnection>(id, ct);
        await RemoveAll<Domain.Documents.MemoryPhoto>(id, ct); await RemoveAll<Domain.Care.Reminder>(id, ct); await RemoveAll<Domain.Care.ReminderLog>(id, ct);
        foreach (var link in await Db.FamilyMembers.Where(f => f.MemberUserId == id).ToListAsync(ct)) { link.MemberUserId = null; link.Status = FamilyMemberStatus.Revoked; }

        var me = await MeAsync(ct);
        Db.Users.Remove(me);
        audit.Record(null, "account.delete", "User", id);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task RemoveAll<T>(Guid userId, CancellationToken ct) where T : class, IUserOwned =>
        Db.Set<T>().RemoveRange(await Db.Set<T>().Where(x => x.UserId == userId).ToListAsync(ct));
}
