using Amma.Domain.Common;

namespace Amma.Domain.Care;

public enum ReminderKind { Water = 0, Meal = 1, Snack = 2, Juice = 3, Tablet = 4, Other = 9 }

/// <summary>A daily reminder (water, meals, snacks, juice, tablets…) shared by everyone who signs in to the account.</summary>
public class Reminder : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public ReminderKind Kind { get; set; }
    public string Title { get; set; } = "";
    /// <summary>E.g. tablet dosage "1 tablet after food", or what to eat.</summary>
    public string? Note { get; set; }
    public List<TimeOnly> Times { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public enum ReminderStatus { Done = 0, Skipped = 1 }

/// <summary>Who completed (or skipped) one reminder occurrence, so both phones can see it.</summary>
public class ReminderLog : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public Guid ReminderId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public ReminderStatus Status { get; set; }
    public Guid? ByLoginId { get; set; }
    public string? ByName { get; set; }
}
