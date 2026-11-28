using Amma.Domain.Common;

namespace Amma.Domain.Users;

public enum UserRole
{
    Mother = 0,
    Partner = 1,
    FamilyViewer = 2,
    Admin = 10,
    MedicalReviewer = 11,
    ContentManager = 12,
    Support = 13,
}

public class User : Entity
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Name { get; set; } = "";
    public string Language { get; set; } = "en";
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public UserRole Role { get; set; } = UserRole.Mother;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
}

/// <summary>
/// A phone/e-mail that can sign in to an account with its own PIN (PIN mode). Several logins can share one
/// account, e.g. a mother and her husband both tracking the same pregnancy, each with their own PIN.
/// </summary>
public class LoginIdentity : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public string Destination { get; set; } = "";
    /// <summary>PBKDF2 hash of this person's PIN.</summary>
    public string? PinHash { get; set; }
    public int FailedPinAttempts { get; set; }
    public DateTimeOffset? PinLockedUntil { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public enum OtpPurpose { Login = 0, FamilyInvite = 1 }

public class OtpCode : Entity
{
    /// <summary>Normalised e-mail or E.164 phone number.</summary>
    public string Destination { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public OtpPurpose Purpose { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public class RefreshToken : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
    /// <summary>Which login (phone) this session belongs to, in PIN mode.</summary>
    public Guid? LoginIdentityId { get; set; }
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}

public enum ConsentType
{
    HealthData = 0,
    FamilySharing = 1,
    LocationSharing = 2,
    AiProcessing = 3,
    Notifications = 4,
}

public class UserConsent : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public ConsentType Type { get; set; }
    public bool Granted { get; set; }
    public string PolicyVersion { get; set; } = "2026-01";
}

public class EmergencyContact : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string Relation { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool IsPrimary { get; set; }
    /// <summary>Notified when the mother taps "Alert family".</summary>
    public bool NotifyInEmergency { get; set; } = true;
}

public class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = "";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
