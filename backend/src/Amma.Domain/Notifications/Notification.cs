using Amma.Domain.Common;

namespace Amma.Domain.Notifications;

public enum NotificationType { Medicine = 0, Appointment = 1, Water = 2, Weekly = 3, Baby = 4, Family = 5, System = 6 }

public class Notification : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    /// <summary>Prevents the same reminder from being generated twice.</summary>
    public string DedupKey { get; set; } = "";
    public DateTimeOffset? ReadAt { get; set; }
}

public class NotificationPreference : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public bool Medicines { get; set; } = true;
    public bool Appointments { get; set; } = true;
    public bool Water { get; set; } = true;
    public bool Weekly { get; set; } = true;
    public bool Baby { get; set; } = true;
    public TimeOnly QuietHoursStart { get; set; } = new(22, 0);
    public TimeOnly QuietHoursEnd { get; set; } = new(7, 0);
}
