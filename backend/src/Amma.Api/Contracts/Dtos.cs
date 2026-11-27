using System.ComponentModel.DataAnnotations;
using Amma.Domain.Care;
using Amma.Domain.Content;
using Amma.Domain.Documents;
using Amma.Domain.Family;
using Amma.Domain.Pregnancy;
using Amma.Domain.Tracking;
using Amma.Domain.Users;

namespace Amma.Api.Contracts;

// ---------- Auth ----------
public record OtpRequest([Required, MaxLength(254)] string Destination);
public record OtpRequestResponse(bool Sent, int ExpiresInSeconds, string? DevCode);
public record OtpVerifyRequest([Required] string Destination, [Required, StringLength(8, MinimumLength = 4)] string Code);
public record AuthConfigResponse(string Mode);
public record PinStatusRequest([Required, MaxLength(254)] string Destination);
public record PinStatusResponse(bool HasPin);
public record PinSetupRequest([Required, MaxLength(254)] string Destination, [Required, MaxLength(128)] string SetupCode, [Required, RegularExpression(@"^\d{4,8}$")] string Pin);
public record PinLoginRequest([Required, MaxLength(254)] string Destination, [Required, MaxLength(8)] string Pin);
public record ChangePinRequest([Required, MaxLength(8)] string CurrentPin, [Required, RegularExpression(@"^\d{4,8}$")] string NewPin);
public record RefreshRequest([Required] string RefreshToken);
public record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, UserDto User, bool IsNewUser);

// ---------- User ----------
public record UserDto(Guid Id, string Name, string? Email, string? Phone, string Language, string TimeZone, UserRole Role, bool HasPregnancy);
public record UpdateMeRequest([MaxLength(120)] string? Name, [RegularExpression("^(en|ta)$")] string? Language, [MaxLength(64)] string? TimeZone);
public record ConsentDto(ConsentType Type, bool Granted, string PolicyVersion, DateTimeOffset UpdatedAt);
public record UpdateConsentRequest(ConsentType Type, bool Granted);
public record EmergencyContactDto(Guid Id, string Name, string Relation, string Phone, bool IsPrimary, bool NotifyInEmergency);
public record SaveEmergencyContactRequest([Required, MaxLength(120)] string Name, [MaxLength(60)] string? Relation, [Required, MaxLength(20)] string Phone, bool IsPrimary, bool NotifyInEmergency = true);
public record EmergencyAlertRequest(double? Latitude, double? Longitude, bool ShareLocation);
public record EmergencyAlertResponse(int NotifiedCount, IReadOnlyList<string> Notified);

// ---------- Pregnancy ----------
public record GestationDto(int TotalDays, int Weeks, int Days, int Trimester, int DaysToGo, int ProgressPercent);
public record PregnancyDto(
    Guid Id, PregnancyStatus Status, DateOnly Lmp, DateOnly Edd, DatingMethod DatingMethod, bool DoctorConfirmed,
    PregnancyType PregnancyType, DietPreference Diet, IReadOnlyList<string> FoodAllergies, IReadOnlyList<string> MedicalConditions,
    string? BloodGroup, string? RhFactor, string? DoctorName, string? DoctorPhone, string? HospitalName, string? HospitalPhone,
    int WaterGoalGlasses, GestationDto Gestation);
public record SavePregnancyRequest(
    DateOnly? Lmp, DateOnly? Edd, DatingMethod DatingMethod = DatingMethod.Lmp, bool DoctorConfirmed = false,
    PregnancyType PregnancyType = PregnancyType.Single, DietPreference Diet = DietPreference.Vegetarian,
    List<string>? FoodAllergies = null, List<string>? MedicalConditions = null,
    [MaxLength(8)] string? BloodGroup = null, [MaxLength(16)] string? RhFactor = null,
    [MaxLength(120)] string? DoctorName = null, [MaxLength(20)] string? DoctorPhone = null,
    [MaxLength(160)] string? HospitalName = null, [MaxLength(20)] string? HospitalPhone = null,
    [Range(4, 20)] int WaterGoalGlasses = 10);

// ---------- Content ----------
public record WeekDto(int Week, string Emoji, string SizeName, string Length, string Weight);
public record WeeklyGuideDto(int FromWeek, int ToWeek, IReadOnlyList<string> Baby, IReadOnlyList<string> Mother, IReadOnlyList<string> Tips, bool Reviewed);
public record FoodDto(Guid Id, string Name, string? NameTa, string Emoji, string Category, FoodSafety Safety, string? Nutrients, string? Benefits,
    string? Preparation, string Precautions, string? TrimesterNotes, IReadOnlyList<string> References, bool Reviewed);
public record MealDto(string Diet, string Region, int Order, string Slot, string Emoji, string Description);
public record ArticleDto(Guid Id, string Slug, string Category, string Emoji, string ColorKey, int ReadMinutes, string Title, IReadOnlyList<string> Paragraphs, bool Reviewed);
public record SnippetDto(string Key, string Kind, int Order, string Title, string? Body, string? Emoji, string? Group, int? Week);
public record ContentBundle(
    string Language, DateTimeOffset GeneratedAt,
    IReadOnlyList<WeekDto> Weeks, IReadOnlyList<WeeklyGuideDto> Guides, IReadOnlyList<FoodDto> Foods, IReadOnlyList<MealDto> Meals,
    IReadOnlyList<ArticleDto> Articles, IReadOnlyList<SnippetDto> Snippets);

// ---------- Medicines ----------
public record MedicationDto(Guid Id, string Name, string Dosage, string? Instructions, string Category, IReadOnlyList<TimeOnly> ReminderTimes,
    DateOnly StartDate, DateOnly? EndDate, DateOnly? RefillOn, string ColorKey, bool IsActive);
public record SaveMedicationRequest(
    [Required, MaxLength(120)] string Name, [Required, MaxLength(120)] string Dosage, [MaxLength(300)] string? Instructions,
    [MaxLength(32)] string? Category, [Required, MinLength(1), MaxLength(8)] List<TimeOnly> ReminderTimes,
    DateOnly? StartDate, DateOnly? EndDate, DateOnly? RefillOn, [MaxLength(16)] string? ColorKey);
public record DoseDto(Guid MedicationId, string Name, string Dosage, string? Instructions, string ColorKey, TimeOnly Time, string Slot, MedicationLogStatus? Status);
public record LogDoseRequest(DateOnly? Date, TimeOnly Time, MedicationLogStatus? Status);
public record MedicationHistoryDto(DateOnly Date, int Scheduled, int Taken, int Skipped);

// ---------- Appointments ----------
public record AppointmentDto(Guid Id, string Title, string? DoctorName, string? Location, DateOnly Date, TimeOnly Time, string? Purpose,
    string? Notes, CheckupCategory Category, AppointmentStatus Status, int ReminderMinutesBefore);
public record SaveAppointmentRequest([Required, MaxLength(160)] string Title, [MaxLength(120)] string? DoctorName, [MaxLength(160)] string? Location,
    DateOnly Date, TimeOnly Time, [MaxLength(500)] string? Purpose, [MaxLength(2000)] string? Notes,
    CheckupCategory Category = CheckupCategory.Consultation, [Range(0, 10080)] int ReminderMinutesBefore = 1440);

// ---------- Tracking ----------
public record WaterDto(DateOnly Date, int Glasses, int Goal);
public record SetWaterRequest(DateOnly? Date, [Range(0, 40)] int Glasses);
public record ChecklistItemDto(string Key, string Title, bool Done);
public record ChecklistDto(DateOnly Date, IReadOnlyList<ChecklistItemDto> Items);
public record SetChecklistRequest(bool Done);
public record BagItemDto(string Key, string Group, string Title, bool Packed);
public record HospitalBagDto(int PercentPacked, IReadOnlyList<BagItemDto> Items);
public record SetBagItemRequest(bool Packed);
public record DailyLogDto(DateOnly Date, int? Mood, decimal? SleepHours, IReadOnlyList<string> Symptoms, string? Journal);
public record SaveDailyLogRequest([Range(0, 4)] int? Mood, [Range(0, 24)] decimal? SleepHours, List<string>? Symptoms, [MaxLength(8000)] string? Journal);

// ---------- Health ----------
public record WeightDto(Guid Id, DateOnly Date, int Week, decimal Kg);
public record AddWeightRequest(DateOnly? Date, [Range(20, 250)] decimal Kg);
public record BloodPressureDto(Guid Id, DateTimeOffset MeasuredAt, int Systolic, int Diastolic, int? Pulse);
public record AddBloodPressureRequest(DateTimeOffset? MeasuredAt, [Range(50, 260)] int Systolic, [Range(30, 180)] int Diastolic, [Range(30, 220)] int? Pulse);
public record BloodSugarDto(Guid Id, DateTimeOffset MeasuredAt, int MgDl, SugarContext Context);
public record AddBloodSugarRequest(DateTimeOffset? MeasuredAt, [Range(20, 600)] int MgDl, SugarContext Context);
/// <summary>Never a diagnosis: a prompt to share a reading with the doctor and, if needed, show emergency options.</summary>
public record HealthAdvisory(string Level, string Title, string Message);
public record SavedReading<T>(T Reading, HealthAdvisory? Advisory);
public record HealthSummaryDto(WeightDto? LatestWeight, BloodPressureDto? LatestBloodPressure, BloodSugarDto? LatestBloodSugar, DailyLogDto Today, int WaterGlasses,
    IReadOnlyList<WeightDto> Weights, IReadOnlyList<BloodPressureDto> BloodPressure, IReadOnlyList<BloodSugarDto> BloodSugar);

public record KickSessionDto(Guid Id, DateTimeOffset StartedAt, DateTimeOffset EndedAt, int Movements, int DurationMinutes, string? Notes);
public record SaveKickSessionRequest(DateTimeOffset StartedAt, DateTimeOffset EndedAt, [Range(1, 500)] int Movements, [MaxLength(500)] string? Notes);

// ---------- Family ----------
public record FamilyMemberDto(Guid Id, string Name, string Relation, string? Phone, FamilyRole Role, FamilyMemberStatus Status, string InviteCode,
    bool ShareAppointments, bool ShareMedicines, bool ShareBabyUpdates, bool ShareHealth, bool ShareReports);
public record InviteFamilyRequest([Required, MaxLength(120)] string Name, [MaxLength(60)] string? Relation, [MaxLength(20)] string? Phone, FamilyRole Role = FamilyRole.FamilyViewer);
public record UpdateFamilyPermissionsRequest(bool? ShareAppointments, bool? ShareMedicines, bool? ShareBabyUpdates, bool? ShareHealth, bool? ShareReports, FamilyRole? Role);
public record SharedJourneyDto(Guid FamilyMemberId, string MotherName, FamilyRole Role, GestationDto? Gestation, DateOnly? Edd, WeekDto? Week,
    IReadOnlyList<AppointmentDto>? UpcomingAppointments, IReadOnlyList<DoseDto>? TodaysMedicines, HospitalBagDto? HospitalBag);

// ---------- Documents ----------
public record DocumentDto(Guid Id, string Title, DocumentCategory Category, DateOnly DocumentDate, string FileName, string ContentType, long SizeBytes, string? Notes, DateTimeOffset UploadedAt);
public record DocumentExplanation(string Title, string Summary, IReadOnlyList<KeyValuePair<string, string>> Terms, IReadOnlyList<string> QuestionsForDoctor, string Disclaimer);

// ---------- Notifications ----------
public record NotificationDto(Guid Id, string Type, string Title, string Body, DateTimeOffset CreatedAt, bool Read);
public record NotificationPreferenceDto(bool Medicines, bool Appointments, bool Water, bool Weekly, bool Baby, TimeOnly QuietHoursStart, TimeOnly QuietHoursEnd);

// ---------- Assistant ----------
public record AskRequest([Required, MaxLength(1000)] string Text);
public record AssistantMessageDto(Guid Id, string Role, string Content, bool IsEmergency, DateTimeOffset CreatedAt);
public record AskResponse(AssistantMessageDto Question, AssistantMessageDto Answer, IReadOnlyList<string> Suggestions);

// ---------- Dashboard ----------
public record DashboardDto(UserDto User, PregnancyDto? Pregnancy, WeekDto? Week, string? TipOfTheDay, IReadOnlyList<DoseDto> TodaysMedicines,
    WaterDto Water, AppointmentDto? NextAppointment, IReadOnlyList<MealDto> Meals, ChecklistDto Checklist, int UnreadNotifications);

// ---------- Admin ----------
public record ContentReviewRequest([MaxLength(2000)] string? Notes, DateOnly? NextReviewDue);
public record AdminContentItem(Guid Id, string Type, string Title, string Status, int Version, DateTimeOffset UpdatedAt, DateTimeOffset? ReviewedAt, string? ReviewNotes);
public record SaveFoodRequest([Required, MaxLength(120)] string Name, string? NameTa, [Required] string Emoji, [Required] string Category, FoodSafety Safety,
    string? Nutrients, string? Benefits, string? Preparation, [Required] string Precautions, string? TrimesterNotes, List<string>? References, List<string>? Tags);
public record SaveArticleRequest([Required, MaxLength(120)] string Slug, [Required] string Category, [Required] string Emoji, string? ColorKey,
    [Range(1, 60)] int ReadMinutes, [Required, MaxLength(200)] string Title, [Required, MinLength(1)] List<string> Paragraphs, int? FromWeek, int? ToWeek);
