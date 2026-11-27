using Amma.Domain.Care;
using Amma.Domain.Notifications;
using Amma.Domain.Pregnancy;
using Amma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Services;

/// <summary>Delivers a notification to a device. Plug in Firebase Cloud Messaging / APNs here.</summary>
public interface IPushSender
{
    Task SendAsync(Notification n, CancellationToken ct);
}

public class NoopPushSender(ILogger<NoopPushSender> log) : IPushSender
{
    public Task SendAsync(Notification n, CancellationToken ct)
    {
        log.LogDebug("In-app notification {Type} for {User}: {Title}", n.Type, n.UserId, n.Title);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Generates medicine, appointment and weekly reminders once a minute. Each reminder has a de-duplication key
/// so restarts or multiple runs never send the same reminder twice.
/// For large scale, move this to a dedicated job runner (e.g. Quartz/Hangfire) with sharding.
/// </summary>
public class ReminderWorker(IServiceScopeFactory scopes, ILogger<ReminderWorker> log, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Reminder run failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AmmaDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IPushSender>();
        var userClock = scope.ServiceProvider.GetRequiredService<UserClock>();

        var users = await db.Users.AsNoTracking().Where(u => u.IsActive).Select(u => new { u.Id, u.TimeZone }).ToListAsync(ct);
        var prefs = await db.NotificationPreferences.AsNoTracking().ToDictionaryAsync(p => p.UserId, ct);
        var created = new List<Notification>();

        foreach (var u in users)
        {
            var pref = prefs.GetValueOrDefault(u.Id) ?? new NotificationPreference();
            var now = userClock.LocalNow(u.TimeZone);
            var today = DateOnly.FromDateTime(now);
            var nowTime = TimeOnly.FromDateTime(now);
            var existing = (await db.Notifications.AsNoTracking().Where(n => n.UserId == u.Id && n.CreatedAt > clock.GetUtcNow().AddDays(-2)).Select(n => n.DedupKey).ToListAsync(ct)).ToHashSet();

            void Add(NotificationType type, string key, string title, string body)
            {
                if (existing.Add(key)) created.Add(new Notification { UserId = u.Id, Type = type, DedupKey = key, Title = title, Body = body });
            }

            if (pref.Medicines)
            {
                var meds = await db.Medications.AsNoTracking().Where(m => m.UserId == u.Id && m.IsActive).ToListAsync(ct);
                var logs = await db.MedicationLogs.AsNoTracking().Where(l => l.UserId == u.Id && l.Date == today).ToListAsync(ct);
                foreach (var m in meds.Where(m => m.IsScheduledOn(today)))
                    foreach (var t in m.ReminderTimes.Where(t => t <= nowTime && nowTime < t.AddMinutes(15)))
                        if (!logs.Any(l => l.MedicationId == m.Id && l.ScheduledTime == t))
                            Add(NotificationType.Medicine, $"med:{m.Id}:{today:yyyyMMdd}:{t:HHmm}", $"{m.Name} at {t:h:mm tt}", $"{m.Dosage} · {m.Instructions ?? "as prescribed"}");
            }

            if (pref.Appointments)
            {
                var appts = await db.Appointments.AsNoTracking().Where(a => a.UserId == u.Id && a.Status == AppointmentStatus.Scheduled && a.Date >= today && a.Date <= today.AddDays(8)).ToListAsync(ct);
                foreach (var a in appts)
                {
                    var at = a.Date.ToDateTime(a.Time);
                    if (now >= at.AddMinutes(-a.ReminderMinutesBefore) && now < at)
                        Add(NotificationType.Appointment, $"appt:{a.Id}:{a.Date:yyyyMMdd}", $"{a.Title} {(a.Date == today ? "today" : "on " + a.Date.ToString("d MMM"))}", $"{a.DoctorName} · {at:h:mm tt}");
                }
            }

            if (pref.Weekly)
            {
                var p = await db.PregnancyProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == u.Id && x.Status == PregnancyStatus.Active, ct);
                if (p is not null && nowTime >= new TimeOnly(8, 0))
                {
                    var ga = PregnancyCalculator.Calculate(p.Lmp, today);
                    if (ga.Days == 0 && ga.Weeks is >= 4 and <= 42)
                        Add(NotificationType.Weekly, $"week:{p.Id}:{ga.Weeks}", $"Welcome to week {ga.Weeks} 💗", "See how your baby is growing this week.");
                }
            }
        }

        if (created.Count == 0) return;
        db.Notifications.AddRange(created);
        await db.SaveChangesAsync(ct);
        foreach (var n in created) await push.SendAsync(n, ct);
        log.LogInformation("Created {Count} reminders", created.Count);
    }
}
