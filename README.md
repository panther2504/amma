# Amma – Pregnancy Companion

An end-to-end pregnancy companion for mothers in India: a week-by-week guide, Indian food guidance, reminders for prescribed medicines and checkups, health tracking, a report vault, family sharing and safety-first emergency help.

This repository implements the **Phase 1 MVP and most of Phase 2** of the [project requirements](docs/requirements.md), using the approved UX prototype as the design.

| Layer | Stack |
|---|---|
| Frontend | React 19 + TypeScript + Vite, TanStack Query; Android app via Capacitor with on-device SQLite |
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

## Family setup: Android app + free server + Neon

How it fits together:

- **Android app** (on both phones). Works offline: everything you've seen is saved in on-device SQLite, daily logging is queued on the phone and synced later, and medicine/appointment reminders are scheduled on the phone itself.
- **Free server on Render**. Runs the API (and the web version). It keeps the database password off your phones. On the free plan it sleeps when idle, so the first sync after a while can take up to a minute; the app keeps working meanwhile.
- **Neon PostgreSQL** holds all the data, including uploaded reports (encrypted).

### 1. Neon (database)
1. In the [Neon console](https://console.neon.tech), create a project named `amma` in region **AWS Asia Pacific (Singapore)**.
2. Open **Connect**, turn **off** "Connection pooling", and copy the connection string (`postgresql://…`). You don't need a Neon API key.

### 2. Render (free server)
1. In [Render](https://dashboard.render.com): **New → Blueprint**, pick this GitHub repository and the branch with this code. Render reads `render.yaml`.
2. Fill in the values it asks for:
   - `DATABASE_URL`: the Neon connection string
   - `SignIn__SetupCode`: a long secret only you two know (needed once, to create your PINs)
   - `Otp__AllowedDestinations__0` and `__1`: your two mobile numbers
3. Deploy. Your server URL looks like `https://amma-xxxx.onrender.com`. Opening it in a browser shows the web version of Amma.

### 3. GitHub (builds the Android app)
In the repository: **Settings → Secrets and variables → Actions**
- **Variables** tab: add `AMMA_API_URL` = your Render URL.
- **Secrets** tab: add `ANDROID_KEYSTORE_BASE64` and `ANDROID_KEYSTORE_PASSWORD` (a one-time signing key, so updates install over the old app).

Then **Actions → Android app → Run workflow**. When it finishes, the APK is on the **Releases** page as `android-latest`.

### 4. Install on your phones
Open the repository's Releases page on each phone, download **Amma.apk** and tap it. Allow "Install unknown apps" for your browser when Android asks. Open Amma, enter your mobile number, create your PIN (with the setup code), and allow notifications for reminders.

To update later, run the workflow again and install the new APK over the old one. Your data stays.

### Server settings reference

| Setting | Purpose |
|---|---|
| `SignIn__Mode=pin` | PIN per person instead of SMS codes |
| `SignIn__SharedAccount=true` | Both numbers use ONE account (both track the same pregnancy), each with their own PIN |
| `SignIn__SetupCode` | Needed once to create a PIN, so nobody else can claim your number |
| `Otp__AllowedDestinations__N` | The only numbers allowed to use the app |
| `Storage__Provider=Database` | Keep uploaded reports in Neon (free hosts have no permanent disk) |
| `Storage__EncryptionKey` | Encrypts reports at rest (generated by Render) |
| `Content__IncludeUnreviewed=true` | Show the starter food guide, weekly guide and articles (labelled "review pending") |

5 wrong PINs lock the account for 15 minutes. Change your PIN under Profile → Settings → Security. The API refuses to start in PIN mode if the number list or setup code is missing.

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
