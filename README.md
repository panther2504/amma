# Amma – Pregnancy Companion

An end-to-end pregnancy companion for mothers in India: a week-by-week guide, Indian food guidance, reminders for prescribed medicines and checkups, health tracking, a report vault, family sharing and safety-first emergency help.

This repository implements the **Phase 1 MVP and most of Phase 2** of the [project requirements](docs/requirements.md), using the approved UX prototype as the design.

| Layer | Stack |
|---|---|
| Frontend | React 19 + TypeScript + Vite, TanStack Query (mobile-first web app / PWA) |
| Backend | ASP.NET Core Web API on **.NET 10**, REST under `/api/v1`, JWT auth |
| Database | **PostgreSQL** via EF Core 10 (Npgsql), code-first migrations |

---

## Quick start (no database needed)

Prerequisites: .NET 10 SDK and Node 20+.

```bash
cd backend/src/Amma.Api && dotnet run
```

```bash
cd frontend && npm install && npm run dev
```

Open http://localhost:5173 and sign in with the demo mobile number **9999999999**. In development the one-time code is shown on screen (no SMS is sent).

Without a connection string the API uses an **in-memory database** (data resets on restart) and seeds starter content plus a demo mother account. API docs: http://localhost:5030/docs.

## Connecting PostgreSQL

Set the connection string, then start the API. **Migrations are applied automatically on startup** and starter content is seeded into an empty database.

```bash
export ConnectionStrings__Amma="Host=<host>;Port=5432;Database=amma;Username=<user>;Password=<password>;SSL Mode=Require"
```

```bash
cd backend/src/Amma.Api && dotnet run
```

Prefer `dotnet user-secrets` or your host's secret store for real credentials. To apply migrations manually instead:

```bash
cd backend && dotnet tool restore && dotnet ef database update -p src/Amma.Infrastructure -s src/Amma.Infrastructure
```

The schema (30 tables) is in `backend/src/Amma.Infrastructure/Persistence/Migrations`. It has been verified against PostgreSQL 16.

## Running tests

```bash
cd backend && dotnet test
```

```bash
cd frontend && npm run lint && npm run typecheck
```

The backend suite (54 tests) covers pregnancy dating, emergency detection, health advisories, OTP sign-in, token rotation, data isolation between users, family-sharing permissions, document upload and account deletion.

## Production build

`npm run build:api` (in `frontend/`) builds the web app into the API's `wwwroot`, so one ASP.NET Core server hosts both.

Before going live, configure these settings (environment variables use `__`, e.g. `Jwt__SigningKey`):

| Setting | Why |
|---|---|
| `ConnectionStrings:Amma` | PostgreSQL connection |
| `Jwt:SigningKey` | 32+ random characters, from a secret store |
| `Storage:EncryptionKey` | Base64 32-byte key: encrypts medical documents at rest (AES-GCM) |
| `Storage:RootPath` | Document storage location (swap `LocalFileStorage` for S3/Azure Blob when scaling) |
| `Otp:ReturnCodeInResponse` | Must be `false` (the default) |
| `Otp:AdminDestinations` | Phone/e-mail promoted to Admin on first login (bootstrap) |
| `Content:IncludeUnreviewed` | Must be `false` (the default) so **only medically approved content** reaches users |
| `Cors:Origins` | Only if the web app is hosted on a different origin |

**Important:** starter content is seeded with status `InReview`. With `IncludeUnreviewed=false`, the food guide, weekly guide and articles stay empty until a medical reviewer approves them and an admin publishes them. Emergency warning signs are also bundled into the app, so they are always available.

## What's implemented

| Area | Highlights |
|---|---|
| Sign-in & profile | Passwordless OTP (Indian mobile or e-mail), rotating refresh tokens, pregnancy profile (LMP or doctor's EDD), diet, allergies, conditions, blood group, doctor and hospital |
| Journey tracker | Week/day/trimester, due date, progress, baby size for weeks 4–40, baby/mother/tips per week, milestones |
| Food & nutrition | Searchable Indian food database (Tamil names, 5 safety classes), veg/egg/non-veg meal plans |
| Medicines | Doses exactly as prescribed, multiple times per day, taken/skipped, history; never suggests or changes medicines |
| Checkups | Appointments with reminder offsets, typical checkup schedule |
| Health tracker | Weight chart, BP and blood sugar with "share with your doctor" advisories (never a diagnosis), mood, sleep, symptoms |
| Kick counter | Session timer and history; no universal target; clear "contact your care team" guidance |
| Report vault | PDF/image upload (15 MB), optional encryption at rest, download audit log, plain-language glossary |
| Family | Partner/viewer invites by phone; the mother toggles each permission; partners get a shared view |
| Emergency | Warning signs, 108/102/doctor calls, alert family (optional location), Tele-MANAS; works offline |
| Ask Amma | Safety-first assistant: emergency detection first (English and Tamil), allergy check, answers from the food database |
| Privacy | Append-only consent records, data export (JSON), full account deletion, audit log |
| Reminders | Background worker creates de-duplicated medicine, appointment and weekly notifications |
| Admin API | Content workflow Draft → InReview → Approved (medical reviewer only) → Published → Retired, with versioning |
| Languages | English and Tamil UI strings; content falls back to English until translations are reviewed |

## Project structure

```
backend/
  src/Amma.Domain          Entities, enums, pregnancy calculator
  src/Amma.Infrastructure  EF Core DbContext, migrations, seed data, file storage
  src/Amma.Api             Controllers, auth, assistant, reminder worker
  tests/Amma.Tests         Unit and API integration tests
frontend/
  src/api                  Typed API client, React Query hooks
  src/screens              Splash, onboarding, sign-in, setup, partner view
  src/tabs                 Home, Baby, Care, Learn, Profile
  src/pages                Food, medicines, checkups, health, kicks, chat, reports, family, emergency…
  src/ui, src/lib          Design-system components, i18n, utilities
```

## Next steps

- **Integrations:** an SMS provider for OTPs and invites (e.g. MSG91), push notifications (FCM), cloud file storage.
- **Admin panel UI** for content review (the API is ready), plus medical review of all starter content.
- **Phase 3:** an LLM behind `IAssistantResponder` (the safety layer stays in front), AI report summaries, Tamil voice.
- **Phase 4–5:** postpartum and newborn modules, hospital/doctor directory, subscriptions, more Indian languages, React Native app.
