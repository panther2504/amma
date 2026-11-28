// Mirrors the API contracts in backend/src/Amma.Api/Contracts/Dtos.cs.
// Dates are ISO "yyyy-MM-dd", times are "HH:mm:ss", enums are camelCase strings.

export type Role = 'mother' | 'partner' | 'familyViewer' | 'admin' | 'medicalReviewer' | 'contentManager' | 'support'
export type Diet = 'vegetarian' | 'eggetarian' | 'nonVegetarian'
export type DatingMethod = 'lmp' | 'doctorEdd' | 'ultrasound'
export type FoodSafety = 'recommended' | 'safeIfPrepared' | 'limit' | 'avoid' | 'consultDoctor'
export type DoseStatus = 'taken' | 'skipped'
export type AppointmentStatus = 'scheduled' | 'completed' | 'cancelled'
export type DocumentCategory = 'ultrasound' | 'blood' | 'urine' | 'prescription' | 'vaccination' | 'discharge' | 'other'
export type FamilyRole = 'partner' | 'familyViewer'
export type ConsentType = 'healthData' | 'familySharing' | 'locationSharing' | 'aiProcessing' | 'notifications'

export interface User { id: string; name: string; email?: string | null; phone?: string | null; language: 'en' | 'ta'; timeZone: string; role: Role; hasPregnancy: boolean }
export interface AuthResponse { accessToken: string; accessTokenExpiresAt: string; refreshToken: string; user: User; isNewUser: boolean }
export interface OtpRequestResponse { sent: boolean; expiresInSeconds: number; devCode?: string | null }

export interface Gestation { totalDays: number; weeks: number; days: number; trimester: 1 | 2 | 3; daysToGo: number; progressPercent: number }
export interface Pregnancy {
  id: string; status: string; lmp: string; edd: string; datingMethod: DatingMethod; doctorConfirmed: boolean; pregnancyType: 'single' | 'twins' | 'multiple'
  diet: Diet; foodAllergies: string[]; medicalConditions: string[]; bloodGroup?: string | null; rhFactor?: string | null
  doctorName?: string | null; doctorPhone?: string | null; hospitalName?: string | null; hospitalPhone?: string | null; waterGoalGlasses: number; gestation: Gestation
}
export type SavePregnancy = Partial<Omit<Pregnancy, 'id' | 'status' | 'gestation' | 'lmp' | 'edd'>> & { lmp?: string | null; edd?: string | null; datingMethod: DatingMethod }

export interface Week { week: number; emoji: string; sizeName: string; length: string; weight: string }
export interface WeeklyGuide { fromWeek: number; toWeek: number; baby: string[]; mother: string[]; tips: string[]; reviewed: boolean }
export interface Food { id: string; name: string; nameTa?: string | null; emoji: string; category: string; safety: FoodSafety; nutrients?: string | null; benefits?: string | null; preparation?: string | null; precautions: string; trimesterNotes?: string | null; references: string[]; reviewed: boolean }
export interface Meal { diet: 'veg' | 'egg' | 'nonveg'; region: string; order: number; slot: string; emoji: string; description: string }
export interface Article { id: string; slug: string; category: string; emoji: string; colorKey: string; readMinutes: number; title: string; paragraphs: string[]; reviewed: boolean }
export interface Snippet { key: string; kind: 'Myth' | 'Faq' | 'WarningSign' | 'Milestone' | 'CheckupType' | 'Activity' | 'DailyChecklist' | 'HospitalBag'; order: number; title: string; body?: string | null; emoji?: string | null; group?: string | null; week?: number | null }
export interface ContentBundle { language: string; generatedAt: string; weeks: Week[]; guides: WeeklyGuide[]; foods: Food[]; meals: Meal[]; articles: Article[]; snippets: Snippet[] }

export interface Medication { id: string; name: string; dosage: string; instructions?: string | null; category: string; reminderTimes: string[]; startDate: string; endDate?: string | null; refillOn?: string | null; colorKey: string; isActive: boolean }
export interface SaveMedication { name: string; dosage: string; instructions?: string; category?: string; reminderTimes: string[]; startDate?: string | null; endDate?: string | null; refillOn?: string | null }
export interface Dose { medicationId: string; name: string; dosage: string; instructions?: string | null; colorKey: string; time: string; slot: 'Morning' | 'Afternoon' | 'Evening' | 'Bedtime'; status?: DoseStatus | null }

export interface Appointment { id: string; title: string; doctorName?: string | null; location?: string | null; date: string; time: string; purpose?: string | null; notes?: string | null; category: string; status: AppointmentStatus; reminderMinutesBefore: number }
export interface SaveAppointment { title: string; doctorName?: string; location?: string; date: string; time: string; purpose?: string; notes?: string; reminderMinutesBefore?: number }

export interface Water { date: string; glasses: number; goal: number }
export interface ChecklistItem { key: string; title: string; done: boolean }
export interface Checklist { date: string; items: ChecklistItem[] }
export interface BagItem { key: string; group: string; title: string; packed: boolean }
export interface HospitalBag { percentPacked: number; items: BagItem[] }
export interface DailyLog { date: string; mood?: number | null; sleepHours?: number | null; symptoms: string[]; journal?: string | null }

export interface WeightReading { id: string; date: string; week: number; kg: number }
export interface BpReading { id: string; measuredAt: string; systolic: number; diastolic: number; pulse?: number | null }
export interface SugarReading { id: string; measuredAt: string; mgDl: number; context: 'fasting' | 'afterMeal' | 'random' }
export interface Advisory { level: 'attention' | 'urgent'; title: string; message: string }
export interface Saved<T> { reading: T; advisory?: Advisory | null }
export interface HealthSummary { latestWeight?: WeightReading | null; latestBloodPressure?: BpReading | null; latestBloodSugar?: SugarReading | null; today: DailyLog; waterGlasses: number; weights: WeightReading[]; bloodPressure: BpReading[]; bloodSugar: SugarReading[] }
export interface KickSession { id: string; startedAt: string; endedAt: string; movements: number; durationMinutes: number; notes?: string | null }

export interface FamilyMember { id: string; name: string; relation: string; phone?: string | null; role: FamilyRole; status: 'invited' | 'active' | 'revoked'; inviteCode: string; shareAppointments: boolean; shareMedicines: boolean; shareBabyUpdates: boolean; shareHealth: boolean; shareReports: boolean }
export interface SharedJourney { familyMemberId: string; motherName: string; role: FamilyRole; gestation?: Gestation | null; edd?: string | null; week?: Week | null; upcomingAppointments?: Appointment[] | null; todaysMedicines?: Dose[] | null; hospitalBag?: HospitalBag | null }

export interface MedicalDocument { id: string; title: string; category: DocumentCategory; documentDate: string; fileName: string; contentType: string; sizeBytes: number; notes?: string | null; uploadedAt: string }
export interface DocumentExplanation { title: string; summary: string; terms: { key: string; value: string }[]; questionsForDoctor: string[]; disclaimer: string }

export interface AppNotification { id: string; type: string; title: string; body: string; createdAt: string; read: boolean }
export interface NotificationPrefs { medicines: boolean; appointments: boolean; water: boolean; weekly: boolean; baby: boolean; quietHoursStart: string; quietHoursEnd: string }
export interface Consent { type: ConsentType; granted: boolean; policyVersion: string; updatedAt: string }
export interface EmergencyContact { id: string; name: string; relation: string; phone: string; isPrimary: boolean; notifyInEmergency: boolean }

export interface AssistantMessage { id: string; role: 'user' | 'assistant'; content: string; isEmergency: boolean; createdAt: string }
export interface AskResponse { question: AssistantMessage; answer: AssistantMessage; suggestions: string[] }

export interface Dashboard { user: User; pregnancy?: Pregnancy | null; week?: Week | null; tipOfTheDay?: string | null; todaysMedicines: Dose[]; water: Water; nextAppointment?: Appointment | null; meals: Meal[]; checklist: Checklist; unreadNotifications: number }

// ---- Family reminders, weight & memories
export type ReminderKind = 'water' | 'meal' | 'snack' | 'juice' | 'tablet' | 'other'
export type ReminderStatus = 'done' | 'skipped'
export interface Reminder { id: string; kind: ReminderKind; title: string; note?: string | null; times: string[]; isActive: boolean; sortOrder: number }
export interface SaveReminder { kind: ReminderKind; title: string; note?: string | null; times: string[] }
export interface Occurrence { reminderId: string; kind: ReminderKind; title: string; note?: string | null; time: string; status?: ReminderStatus | null; byName?: string | null; at?: string | null }
export interface Day { date: string; items: Occurrence[]; done: number; total: number }
export interface Person { name: string; phone?: string | null; pinLogin: boolean }
export interface Memory { id: string; takenOn: string; caption?: string | null; pregnancyWeek?: number | null; thumbnail: string; sizeBytes: number; byName?: string | null; createdAt: string }
