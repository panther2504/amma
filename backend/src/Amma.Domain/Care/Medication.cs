using Amma.Domain.Common;

namespace Amma.Domain.Care;

/// <summary>
/// A medicine or supplement exactly as prescribed by the user's healthcare professional.
/// The application never suggests, changes or stops medicines.
/// </summary>
public class Medication : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Free text as written on the prescription, e.g. "1 tablet".</summary>
    public string Dosage { get; set; } = "";
    public string? Instructions { get; set; }
    public string Category { get; set; } = "Other"; // FolicAcid, Iron, Calcium, VitaminD, Other
    public List<TimeOnly> ReminderTimes { get; set; } = [];
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? RefillOn { get; set; }
    public string ColorKey { get; set; } = "sage";
    public bool IsActive { get; set; } = true;

    public bool IsScheduledOn(DateOnly date) => IsActive && date >= StartDate && (EndDate is null || date <= EndDate);
}

public enum MedicationLogStatus { Taken = 0, Skipped = 1 }

public class MedicationLog : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public Guid MedicationId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly ScheduledTime { get; set; }
    public MedicationLogStatus Status { get; set; }
}

public enum AppointmentStatus { Scheduled = 0, Completed = 1, Cancelled = 2 }

public enum CheckupCategory
{
    Consultation = 0, DatingScan = 1, FirstTrimesterScreening = 2, AnomalyScan = 3, GrowthScan = 4,
    GlucoseScreening = 5, BloodTest = 6, UrineTest = 7, Immunization = 8, ThirdTrimesterAssessment = 9,
    DeliveryPreparation = 10, Other = 99,
}

public class Appointment : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = "";
    public string? DoctorName { get; set; }
    public string? Location { get; set; }
    /// <summary>Local date/time in the user's time zone.</summary>
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
    public CheckupCategory Category { get; set; } = CheckupCategory.Consultation;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public int ReminderMinutesBefore { get; set; } = 24 * 60;
}
