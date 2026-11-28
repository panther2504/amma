using Amma.Domain.Assistant;
using Amma.Domain.Care;
using Amma.Domain.Common;
using Amma.Domain.Content;
using Amma.Domain.Documents;
using Amma.Domain.Family;
using Amma.Domain.Notifications;
using Amma.Domain.Pregnancy;
using Amma.Domain.Tracking;
using Amma.Domain.Users;
using Amma.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Amma.Infrastructure.Persistence;

public class AmmaDbContext(DbContextOptions<AmmaDbContext> options) : DbContext(options)
{
    // Users
    public DbSet<User> Users => Set<User>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<LoginIdentity> LoginIdentities => Set<LoginIdentity>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Pregnancy
    public DbSet<PregnancyProfile> PregnancyProfiles => Set<PregnancyProfile>();

    // Care
    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<MedicationLog> MedicationLogs => Set<MedicationLog>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();
    public DbSet<MemoryPhoto> MemoryPhotos => Set<MemoryPhoto>();
    public DbSet<DriveConnection> DriveConnections => Set<DriveConnection>();

    // Tracking
    public DbSet<WaterLog> WaterLogs => Set<WaterLog>();
    public DbSet<WeightLog> WeightLogs => Set<WeightLog>();
    public DbSet<BloodPressureLog> BloodPressureLogs => Set<BloodPressureLog>();
    public DbSet<BloodSugarLog> BloodSugarLogs => Set<BloodSugarLog>();
    public DbSet<DailyLog> DailyLogs => Set<DailyLog>();
    public DbSet<KickSession> KickSessions => Set<KickSession>();
    public DbSet<HospitalBagCheck> HospitalBagChecks => Set<HospitalBagCheck>();

    // Family, documents, notifications, assistant
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<MedicalDocument> MedicalDocuments => Set<MedicalDocument>();
    public DbSet<DocumentAccessLog> DocumentAccessLogs => Set<DocumentAccessLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<AssistantMessage> AssistantMessages => Set<AssistantMessage>();

    // Infrastructure
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<AppSecret> AppSecrets => Set<AppSecret>();

    // Content
    public DbSet<PregnancyWeek> PregnancyWeeks => Set<PregnancyWeek>();
    public DbSet<WeeklyGuide> WeeklyGuides => Set<WeeklyGuide>();
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<MealPlanItem> MealPlanItems => Set<MealPlanItem>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ContentSnippet> ContentSnippets => Set<ContentSnippet>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Store enums as readable strings.
        b.Properties<UserRole>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<OtpPurpose>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<ConsentType>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<ContentStatus>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<DatingMethod>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<PregnancyType>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<DietPreference>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<PregnancyStatus>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<MedicationLogStatus>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<AppointmentStatus>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<CheckupCategory>().HaveConversion<string>().HaveMaxLength(48);
        b.Properties<SugarContext>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<FamilyRole>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<FamilyMemberStatus>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<DocumentCategory>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<NotificationType>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<FoodSafety>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<SnippetKind>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<ReminderKind>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<ReminderStatus>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<string>().HaveMaxLength(2000);
        b.Properties<decimal>().HavePrecision(8, 2);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Phone).IsUnique();
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(120);
        });
        m.Entity<LoginIdentity>(e => { e.HasIndex(x => x.Destination).IsUnique(); e.HasIndex(x => x.UserId); e.Property(x => x.Destination).HasMaxLength(254); });
        m.Entity<OtpCode>().HasIndex(x => new { x.Destination, x.Purpose, x.CreatedAt });
        m.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        m.Entity<RefreshToken>().HasIndex(x => x.UserId);
        m.Entity<UserConsent>().HasIndex(x => new { x.UserId, x.Type });
        m.Entity<EmergencyContact>().HasIndex(x => x.UserId);
        m.Entity<AuditLog>().HasIndex(x => new { x.UserId, x.At });

        m.Entity<PregnancyProfile>().HasIndex(x => new { x.UserId, x.Status });

        m.Entity<Medication>().HasIndex(x => new { x.UserId, x.IsActive });
        m.Entity<MedicationLog>().HasIndex(x => new { x.MedicationId, x.Date, x.ScheduledTime }).IsUnique();
        m.Entity<MedicationLog>().HasIndex(x => new { x.UserId, x.Date });
        m.Entity<Appointment>().HasIndex(x => new { x.UserId, x.Date });
        m.Entity<Reminder>().HasIndex(x => new { x.UserId, x.IsActive });
        m.Entity<ReminderLog>().HasIndex(x => new { x.ReminderId, x.Date, x.Time }).IsUnique();
        m.Entity<ReminderLog>().HasIndex(x => new { x.UserId, x.Date });
        m.Entity<MemoryPhoto>().HasIndex(x => new { x.UserId, x.TakenOn });
        m.Entity<DriveConnection>().HasIndex(x => x.UserId).IsUnique();
        m.Entity<DriveConnection>().Property(x => x.ClientId).HasMaxLength(200);
        m.Entity<MemoryPhoto>().Property(x => x.ThumbnailDataUrl).HasMaxLength(200_000);

        m.Entity<WaterLog>().HasIndex(x => new { x.UserId, x.Date }).IsUnique();
        m.Entity<DailyLog>().HasIndex(x => new { x.UserId, x.Date }).IsUnique();
        m.Entity<DailyLog>().Property(x => x.Journal).HasMaxLength(8000);
        m.Entity<WeightLog>().HasIndex(x => new { x.UserId, x.Date });
        m.Entity<BloodPressureLog>().HasIndex(x => new { x.UserId, x.MeasuredAt });
        m.Entity<BloodSugarLog>().HasIndex(x => new { x.UserId, x.MeasuredAt });
        m.Entity<KickSession>().HasIndex(x => new { x.UserId, x.StartedAt });
        m.Entity<HospitalBagCheck>().HasIndex(x => new { x.UserId, x.ItemKey }).IsUnique();

        m.Entity<FamilyMember>().HasIndex(x => x.UserId);
        m.Entity<FamilyMember>().HasIndex(x => x.MemberUserId);
        m.Entity<FamilyMember>().HasIndex(x => x.InviteCode).IsUnique();
        m.Entity<MedicalDocument>().HasIndex(x => new { x.UserId, x.DocumentDate });
        m.Entity<DocumentAccessLog>().HasIndex(x => x.DocumentId);
        m.Entity<Notification>().HasIndex(x => new { x.UserId, x.DedupKey }).IsUnique();
        m.Entity<Notification>().HasIndex(x => new { x.UserId, x.CreatedAt });
        m.Entity<NotificationPreference>().HasIndex(x => x.UserId).IsUnique();
        m.Entity<AssistantMessage>().HasIndex(x => new { x.UserId, x.CreatedAt });
        m.Entity<AssistantMessage>().Property(x => x.Content).HasMaxLength(8000);

        m.Entity<StoredFile>(e => { e.HasKey(x => x.Key); e.Property(x => x.Key).HasMaxLength(32); });
        m.Entity<AppSecret>(e => { e.HasKey(x => x.Name); e.Property(x => x.Name).HasMaxLength(64); });
        m.Entity<IdempotencyRecord>(e =>
        {
            e.HasKey(x => new { x.UserId, x.Key });
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Body).HasMaxLength(65536);
            e.HasIndex(x => x.CreatedAt);
        });

        m.Entity<PregnancyWeek>().HasIndex(x => new { x.Week, x.Language }).IsUnique();
        m.Entity<WeeklyGuide>().HasIndex(x => new { x.FromWeek, x.Language });
        m.Entity<FoodItem>().HasIndex(x => new { x.Language, x.Safety });
        m.Entity<FoodItem>().Property(x => x.Name).HasMaxLength(120);
        m.Entity<MealPlanItem>().HasIndex(x => new { x.Diet, x.Region, x.Language });
        m.Entity<Article>().HasIndex(x => new { x.Slug, x.Language }).IsUnique();
        m.Entity<ContentSnippet>().HasIndex(x => new { x.Kind, x.Language, x.Order });
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = now;
            if (entry.State is EntityState.Added or EntityState.Modified) entry.Entity.UpdatedAt = now;
        }
        return base.SaveChangesAsync(ct);
    }
}

/// <summary>Remembers the response to a write so a retried request (e.g. replayed after being offline) is not applied twice.</summary>
public class IdempotencyRecord
{
    public Guid UserId { get; set; }
    public string Key { get; set; } = "";
    public int StatusCode { get; set; }
    public string? ContentType { get; set; }
    public string? Body { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A server-generated secret (e.g. the Android signing key), stored encrypted with Storage:EncryptionKey.</summary>
public class AppSecret
{
    public string Name { get; set; } = "";
    public byte[] Value { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
