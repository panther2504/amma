using Amma.Api.Contracts;
using Amma.Domain.Care;
using Amma.Domain.Common;
using Amma.Domain.Content;
using Amma.Domain.Pregnancy;
using Amma.Domain.Users;
using Amma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amma.Api.Services;

/// <summary>Resolves "today" and "now" in the user's own time zone (default Asia/Kolkata).</summary>
public class UserClock(TimeProvider clock)
{
    public DateTimeOffset UtcNow => clock.GetUtcNow();

    public DateTime LocalNow(string timeZone)
    {
        var tz = Find(timeZone);
        return TimeZoneInfo.ConvertTime(clock.GetUtcNow(), tz).DateTime;
    }

    public DateOnly Today(string timeZone) => DateOnly.FromDateTime(LocalNow(timeZone));

    public static TimeZoneInfo Find(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
    }
}

public class ContentOptions
{
    /// <summary>
    /// When true, content still awaiting medical review is shown (flagged as unreviewed).
    /// Must be false in production so only Published content reaches mothers.
    /// </summary>
    public bool IncludeUnreviewed { get; set; }
}

public class ContentQuery(AmmaDbContext db, IOptions<ContentOptions> options)
{
    private readonly bool _all = options.Value.IncludeUnreviewed;

    public IQueryable<T> Visible<T>(string lang = "en") where T : ReviewableContent
    {
        var q = db.Set<T>().AsNoTracking().Where(x => x.Language == lang);
        return _all
            ? q.Where(x => x.Status == ContentStatus.Published || x.Status == ContentStatus.Approved || x.Status == ContentStatus.InReview)
            : q.Where(x => x.Status == ContentStatus.Published);
    }

    public async Task<PregnancyWeek?> WeekAsync(int gestationalWeek, CancellationToken ct)
    {
        var w = Math.Clamp(gestationalWeek, 4, 40);
        return await Visible<PregnancyWeek>().FirstOrDefaultAsync(x => x.Week == w, ct);
    }

    public async Task<WeeklyGuide?> GuideAsync(int gestationalWeek, CancellationToken ct)
    {
        var w = Math.Clamp(gestationalWeek, 4, 42);
        return await Visible<WeeklyGuide>().FirstOrDefaultAsync(x => x.FromWeek <= w && x.ToWeek >= w, ct);
    }

    public Task<List<ContentSnippet>> SnippetsAsync(SnippetKind kind, CancellationToken ct) =>
        Visible<ContentSnippet>().Where(s => s.Kind == kind).OrderBy(s => s.Order).ToListAsync(ct);

    public static string DietKey(DietPreference d) => d switch
    {
        DietPreference.Eggetarian => "egg",
        DietPreference.NonVegetarian => "nonveg",
        _ => "veg",
    };
}

public class AuditService(AmmaDbContext db, IHttpContextAccessor http)
{
    public void Record(Guid? userId, string action, string? entityType = null, object? entityId = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = action, EntityType = entityType, EntityId = entityId?.ToString(),
            IpAddress = http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        });
}

/// <summary>Builds the list of doses scheduled for a date from the user's prescribed medicines.</summary>
public class DoseService(AmmaDbContext db)
{
    public async Task<List<DoseDto>> ForDateAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        var meds = await db.Medications.AsNoTracking().Where(m => m.UserId == userId && m.IsActive).ToListAsync(ct);
        var logs = await db.MedicationLogs.AsNoTracking().Where(l => l.UserId == userId && l.Date == date).ToListAsync(ct);
        return meds.Where(m => m.IsScheduledOn(date))
            .SelectMany(m => m.ReminderTimes.Select(t => new DoseDto(
                m.Id, m.Name, m.Dosage, m.Instructions, m.ColorKey, t, Mapping.SlotFor(t),
                logs.FirstOrDefault(l => l.MedicationId == m.Id && l.ScheduledTime == t)?.Status)))
            .OrderBy(d => d.Time).ToList();
    }
}

/// <summary>
/// Flags readings that should be shared with the doctor. Uses commonly published reference thresholds
/// (configurable after clinical review). It never diagnoses.
/// </summary>
public static class HealthAdvisor
{
    public static HealthAdvisory? ForBloodPressure(int systolic, int diastolic)
    {
        if (systolic >= 160 || diastolic >= 110)
            return new("urgent", "Please contact your doctor now",
                $"Your reading of {systolic}/{diastolic} is much higher than the commonly used reference range. Amma cannot diagnose — please contact your doctor or maternity unit now. If you also have a severe headache, vision changes, upper tummy pain or sudden swelling, seek emergency care immediately.");
        if (systolic >= 140 || diastolic >= 90)
            return new("attention", "Please share this reading with your doctor",
                $"Your reading of {systolic}/{diastolic} is higher than the commonly used reference range. Amma cannot diagnose — please contact your doctor today. If you also have a severe headache, vision changes, upper tummy pain or sudden swelling, seek medical care immediately.");
        return null;
    }

    public static HealthAdvisory? ForBloodSugar(int mgDl, Domain.Tracking.SugarContext context)
    {
        if (mgDl < 60)
            return new("urgent", "Low sugar reading", "This reading is low. If you feel shaky, sweaty, confused or faint, take some sugar or juice and contact your doctor right away.");
        var high = context == Domain.Tracking.SugarContext.Fasting ? mgDl >= 95 : mgDl >= 140;
        return high
            ? new("attention", "Please share this reading with your doctor", "This reading is above the commonly used reference range for pregnancy. Amma cannot diagnose — please discuss it with your doctor.")
            : null;
    }
}
