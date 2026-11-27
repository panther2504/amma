using Amma.Api.Contracts;
using Amma.Domain.Care;
using Amma.Domain.Common;
using Amma.Domain.Content;
using Amma.Domain.Documents;
using Amma.Domain.Family;
using Amma.Domain.Pregnancy;
using Amma.Domain.Tracking;
using Amma.Domain.Users;

namespace Amma.Api.Services;

public static class Mapping
{
    public static UserDto ToDto(this User u, bool hasPregnancy) => new(u.Id, u.Name, u.Email, u.Phone, u.Language, u.TimeZone, u.Role, hasPregnancy);

    public static GestationDto ToDto(this GestationalAge g) => new(g.TotalDays, g.Weeks, g.Days, g.Trimester, g.DaysToGo, g.ProgressPercent);

    public static PregnancyDto ToDto(this PregnancyProfile p, DateOnly today) => new(
        p.Id, p.Status, p.Lmp, p.Edd, p.DatingMethod, p.DoctorConfirmed, p.PregnancyType, p.Diet, p.FoodAllergies, p.MedicalConditions,
        p.BloodGroup, p.RhFactor, p.DoctorName, p.DoctorPhone, p.HospitalName, p.HospitalPhone, p.WaterGoalGlasses,
        PregnancyCalculator.Calculate(p.Lmp, today).ToDto());

    public static bool IsReviewed(this ReviewableContent c) => c.Status == ContentStatus.Published;

    public static WeekDto ToDto(this PregnancyWeek w) => new(w.Week, w.Emoji, w.SizeName, w.Length, w.Weight);
    public static WeeklyGuideDto ToDto(this WeeklyGuide g) => new(g.FromWeek, g.ToWeek, g.Baby, g.Mother, g.Tips, g.IsReviewed());
    public static FoodDto ToDto(this FoodItem f) => new(f.Id, f.Name, f.NameTa, f.Emoji, f.Category, f.Safety, f.Nutrients, f.Benefits, f.Preparation, f.Precautions, f.TrimesterNotes, f.References, f.IsReviewed());
    public static MealDto ToDto(this MealPlanItem m) => new(m.Diet, m.Region, m.Order, m.Slot, m.Emoji, m.Description);
    public static ArticleDto ToDto(this Article a) => new(a.Id, a.Slug, a.Category, a.Emoji, a.ColorKey, a.ReadMinutes, a.Title, a.Paragraphs, a.IsReviewed());
    public static SnippetDto ToDto(this ContentSnippet s) => new(s.Key, s.Kind.ToString(), s.Order, s.Title, s.Body, s.Emoji, s.Group, s.Week);

    public static MedicationDto ToDto(this Medication m) => new(m.Id, m.Name, m.Dosage, m.Instructions, m.Category, m.ReminderTimes.Order().ToList(), m.StartDate, m.EndDate, m.RefillOn, m.ColorKey, m.IsActive);
    public static AppointmentDto ToDto(this Appointment a) => new(a.Id, a.Title, a.DoctorName, a.Location, a.Date, a.Time, a.Purpose, a.Notes, a.Category, a.Status, a.ReminderMinutesBefore);
    public static WeightDto ToDto(this WeightLog w) => new(w.Id, w.Date, w.GestationalWeek, w.Kg);
    public static BloodPressureDto ToDto(this BloodPressureLog b) => new(b.Id, b.MeasuredAt, b.Systolic, b.Diastolic, b.Pulse);
    public static BloodSugarDto ToDto(this BloodSugarLog b) => new(b.Id, b.MeasuredAt, b.MgDl, b.Context);
    public static DailyLogDto ToDto(this DailyLog? d, DateOnly date) => d is null ? new(date, null, null, [], null) : new(d.Date, d.Mood, d.SleepHours, d.Symptoms, d.Journal);
    public static KickSessionDto ToDto(this KickSession k) => new(k.Id, k.StartedAt, k.EndedAt, k.Movements, Math.Max(1, (int)Math.Round((k.EndedAt - k.StartedAt).TotalMinutes)), k.Notes);
    public static FamilyMemberDto ToDto(this FamilyMember f) => new(f.Id, f.Name, f.Relation, f.Phone, f.Role, f.Status, f.InviteCode, f.ShareAppointments, f.ShareMedicines, f.ShareBabyUpdates, f.ShareHealth, f.ShareReports);
    public static DocumentDto ToDto(this MedicalDocument d) => new(d.Id, d.Title, d.Category, d.DocumentDate, d.FileName, d.ContentType, d.SizeBytes, d.Notes, d.CreatedAt);
    public static EmergencyContactDto ToDto(this EmergencyContact c) => new(c.Id, c.Name, c.Relation, c.Phone, c.IsPrimary, c.NotifyInEmergency);

    public static string SlotFor(TimeOnly t) => t.Hour < 12 ? "Morning" : t.Hour < 16 ? "Afternoon" : t.Hour < 20 ? "Evening" : "Bedtime";
}
