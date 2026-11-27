using Amma.Domain.Common;

namespace Amma.Domain.Pregnancy;

public enum DatingMethod { Lmp = 0, DoctorEdd = 1, Ultrasound = 2 }
public enum PregnancyType { Single = 0, Twins = 1, Multiple = 2 }
public enum DietPreference { Vegetarian = 0, Eggetarian = 1, NonVegetarian = 2 }
public enum PregnancyStatus { Active = 0, Delivered = 1, Ended = 2 }

public class PregnancyProfile : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public PregnancyStatus Status { get; set; } = PregnancyStatus.Active;

    /// <summary>First day of the last menstrual period (or the equivalent derived from the confirmed due date).</summary>
    public DateOnly Lmp { get; set; }
    public DateOnly Edd { get; set; }
    public DatingMethod DatingMethod { get; set; } = DatingMethod.Lmp;
    public bool DoctorConfirmed { get; set; }

    public PregnancyType PregnancyType { get; set; } = PregnancyType.Single;
    public DietPreference Diet { get; set; } = DietPreference.Vegetarian;
    public List<string> FoodAllergies { get; set; } = [];
    public List<string> MedicalConditions { get; set; } = [];
    public string? BloodGroup { get; set; }
    public string? RhFactor { get; set; }

    public string? DoctorName { get; set; }
    public string? DoctorPhone { get; set; }
    public string? HospitalName { get; set; }
    public string? HospitalPhone { get; set; }

    public int WaterGoalGlasses { get; set; } = 10;
    public DateOnly? DeliveryDate { get; set; }
}
