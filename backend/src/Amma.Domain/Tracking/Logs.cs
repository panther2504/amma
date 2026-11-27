using Amma.Domain.Common;

namespace Amma.Domain.Tracking;

public class WaterLog : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public int Glasses { get; set; }
}

public class WeightLog : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public int GestationalWeek { get; set; }
    public decimal Kg { get; set; }
}

public class BloodPressureLog : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public DateTimeOffset MeasuredAt { get; set; }
    public int Systolic { get; set; }
    public int Diastolic { get; set; }
    public int? Pulse { get; set; }
}

public enum SugarContext { Fasting = 0, AfterMeal = 1, Random = 2 }

public class BloodSugarLog : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public DateTimeOffset MeasuredAt { get; set; }
    public int MgDl { get; set; }
    public SugarContext Context { get; set; }
}

/// <summary>One row per day for mood, sleep, symptoms and a free-text journal.</summary>
public class DailyLog : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public int? Mood { get; set; } // 0 (very low) .. 4 (great)
    public decimal? SleepHours { get; set; }
    public List<string> Symptoms { get; set; } = [];
    public string? Journal { get; set; }
    public List<string> ChecklistDone { get; set; } = [];
}

public class KickSession : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public int Movements { get; set; }
    public string? Notes { get; set; }
}

public class HospitalBagCheck : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public string ItemKey { get; set; } = "";
    public bool Packed { get; set; }
}
