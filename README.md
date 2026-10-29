# PHCIS Clinic Booking System

A simplified clinic booking system for the **Primary Health Care Information System (PHCIS)**. Patients find a
clinic, see open time slots, book an appointment and get an instant confirmation with a reference number. They can
also cancel or reschedule. Clinic staff open slots and see each day's appointment list.

Built with **Blazor WebAssembly + ASP.NET Core Web API + EF Core + PostgreSQL**, following a layered (Clean
Architecture) design.

| Assessment task | Where to find it |
|---|---|
| 1. Requirements analysis (stakeholders, functional, non-functional) | [docs/01-requirements.md](docs/01-requirements.md) |
| 2. System design (architecture, data model, ERD, sequence and flow diagrams) | [docs/02-system-design.md](docs/02-system-design.md) |
| 3. Implementation (booking, availability, confirmation, no double bookings) | `src/` (see [Solution structure](#solution-structure)) |
| 4. Code quality (SOLID, naming, docs, unit tests) | `tests/`, XML doc comments throughout, [design notes](docs/02-system-design.md#21-architecture) |

---

## Quick start

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker.

```bash
# 1. Start PostgreSQL (port 5433)
docker compose up -d db

# 2. Run the app (API + Blazor client on one origin). Migrations and demo data are applied on startup.
dotnet run --project src/ClinicBooking.Api --launch-profile http
```

Open **http://localhost:5072**. Swagger UI is at **http://localhost:5072/swagger**, and the health check at `/health`.

Or run everything in containers:

```bash
docker compose up --build      # app on http://localhost:8080
```

### Demo accounts (Development only)

Seeded from `appsettings.Development.json` (`Seed:DemoPassword`, default `Demo@12345`):

| Email | Role | Can do |
|---|---|---|
| `patient@phcis.local` | Patient | Book, view, cancel, reschedule own appointments |
| `staff@phcis.local` | Staff | Daily schedule per clinic, open slots, cancel any appointment |
| `admin@phcis.local` | Admin | Everything staff can, plus `POST /api/clinics` |

You can also register a new patient from the UI. Three demo clinics are seeded, each with 30-minute slots (2 patients
per slot) on weekdays for the next 14 days.

### Run the tests

```bash
dotnet test
```

48 tests:

- **43 unit tests** (xUnit + Moq) cover the booking rules, domain invariants, slot generation and validators.
- **5 bUnit component tests** cover the slot picker UI.

---

## What it does

**Patients**
- Register (SA ID numbers are checked with the Luhn algorithm; passports are accepted) and sign in.
- Browse and search clinics, pick a day and see only the slots that are still bookable.
- Book with an optional reason. A confirmation page shows the reference number (e.g. `PHC-885CGHGB`) and a confirmation message, and can be printed.
- See upcoming and past/cancelled appointments; cancel, or reschedule while keeping the same reference.

**Clinic staff / admin**
- Daily appointment list per clinic, with patient contact details and booked/cancelled counts.
- Open slots for a date range: opening hours, slot length, patients per slot, weekends on or off. Overlapping existing slots is rejected.
- Cancel any appointment (e.g. a clinic closure).

### Booking rules (enforced on the server)

| Rule | Result |
|---|---|
| Slot is full (`BookedCount == Capacity`) | `409 Conflict` "fully booked" |
| Two people book the last place at the same moment | Only one succeeds. Optimistic concurrency on `TimeSlot.BookedCount` → the other gets `409` |
| Patient already has an appointment overlapping that time | `409` "You already have an appointment at this time" |
| Slot has started or is in the past | `422` |
| Cancelling or rescheduling a started or already-cancelled appointment | `422` |
| Patient accesses another patient's appointment | `404`, so IDs cannot be probed |
| Invalid input | `400` with per-field messages; the same FluentValidation rules run in the browser first |

There are database-level backstops as well:

- A check constraint `0 <= BookedCount <= Capacity`.
- A filtered unique index on `(PatientId, TimeSlotId)` for active bookings.

These rules were verified against the running app, including firing 4 simultaneous bookings at one remaining place:
exactly one got `201` and three got `409`.

---

## Architecture

```
Browser ── Blazor WASM (MudBlazor) ──HTTPS+JWT──▶ ASP.NET Core API ──▶ Application ──▶ Domain
                                                     │                    ▲
                                                     └── Infrastructure ──┘ (EF Core, Identity, JWT) ──▶ PostgreSQL
                 Contracts (DTOs + validators) shared by client and server
```

The full diagrams are in [docs/02-system-design.md](docs/02-system-design.md):

- Architecture
- ERD
- Booking sequence diagram
- Validation flowchart
- Appointment state machine

### Solution structure

```
src/
  ClinicBooking.Domain          Entities + invariants (Clinic, Patient, TimeSlot, Appointment), domain exceptions
  ClinicBooking.Contracts       DTOs and FluentValidation validators shared with the Blazor client
  ClinicBooking.Application     Use-case services (AppointmentService, TimeSlotService, ClinicService),
                                repository/clock/user abstractions, SlotGenerator, mapping
  ClinicBooking.Infrastructure  EF Core DbContext + configurations + migrations, repositories, ASP.NET Identity,
                                JWT generation, clinic clock, demo data seeder
  ClinicBooking.Api             Controllers, JWT auth, global problem-details error handler, rate limiting,
                                Serilog, Swagger, health checks; also hosts the WASM client
  ClinicBooking.Client          Blazor WebAssembly UI (MudBlazor), JWT auth state, typed API clients
tests/
  ClinicBooking.UnitTests       xUnit + Moq: booking/cancel/reschedule rules, domain, slot generation, validators
  ClinicBooking.Client.Tests    bUnit: SlotPicker component
```

### Technology choices

| Area | Choice | Notes |
|---|---|---|
| Frontend | Blazor WebAssembly, **MudBlazor** | Responsive Material UI; tables collapse to cards on phones |
| Validation | **FluentValidation** | Validators live in `Contracts`, so the browser and the API run identical rules |
| API | ASP.NET Core Web API (controllers), **Swagger/OpenAPI** | XML comments feed Swagger; JWT "Authorize" button included |
| Data | **EF Core 10 + PostgreSQL** (Npgsql), **EF Core Migrations** | Migrations applied on startup |
| Auth | **ASP.NET Core Identity** + **JWT** bearer tokens | Roles: Patient, Staff, Admin; 2-hour tokens; rate-limited sign-in |
| Logging | **Serilog** | Console + daily rolling files, request logging, structured booking events |
| Errors | RFC 7807 problem details via `IExceptionHandler` | One consistent error shape for the client |
| Testing | **xUnit, Moq, bUnit** | |
| DevOps | **Docker** (multi-stage), docker-compose, **GitHub Actions** | CI builds, runs tests and builds the image |
| Mapping | Hand-written extension methods instead of AutoMapper | Mappings are few and simple; compile-time safe and easy to debug. AutoMapper 15+ also requires a commercial licence, and older versions have a published vulnerability (GHSA-rvv3-g6hj-g44x). |
| Dapper | Not used | No query needed it at this scale; EF Core `AsNoTracking` projections are sufficient. It would be the first step for reporting queries. |

### Key decisions

- **Clean/layered monolith over microservices.** One bounded context does not justify the operational cost of microservices. The layers keep a later split possible.
- **Capacity per slot.** Parallel consulting rooms are common in clinics.
- **Clinic-local times.** Slots are stored as local wall-clock times (`Africa/Johannesburg`, configurable). "Now" comes from an injectable `IClinicClock`, which also makes the time rules unit-testable.
- **Soft cancellation.** Appointments keep their status history for reporting and auditing.
- **Single origin.** The API hosts the WASM client: no CORS configuration, and one container to deploy.

---

## Configuration

| Key | Purpose | Development value |
|---|---|---|
| `ConnectionStrings:ClinicBooking` | PostgreSQL connection | `Host=localhost;Port=5433;…` |
| `Jwt:Key` | Token signing key (≥ 32 chars) | Dev-only key. **Set via environment variable or secret store in production** (`Jwt__Key`) |
| `Jwt:ExpiryMinutes` | Token lifetime | 120 |
| `Clinic:TimeZone` | Clinics' time zone | `Africa/Johannesburg` |
| `Seed:Enabled` / `Seed:DemoPassword` | Demo data | `true` / `Demo@12345` |

To add a migration after changing the model:

```bash
dotnet tool restore
dotnet ef migrations add <Name> -p src/ClinicBooking.Infrastructure -s src/ClinicBooking.Api -o Persistence/Migrations
```

---

## Assumptions

1. All clinics share one time zone (configurable).
2. A slot can hold several patients (`Capacity`, default 1 for staff-created slots).
3. The confirmation is shown on screen and can be printed. Sending it by SMS or email is an enhancement.
4. The UI reschedules within the same clinic; the API allows any clinic.

## With more time

- **Reminders and confirmations by SMS/email** (Twilio/SendGrid) via an `INotificationSender`, sent from an outbox table so a failed send never loses a booking.
- **Real-time availability** with SignalR, so slots disappear for other viewers the moment they are booked.
- **Calendar sync:** an `.ics` download is a quick win; Outlook/Google APIs would follow.
- Mark appointments **Completed / No-show**, plus no-show reporting per clinic and district.
- **Integration tests** with `WebApplicationFactory` + Testcontainers (PostgreSQL), and Playwright end-to-end tests.
- Refresh tokens and an HttpOnly-cookie BFF for token storage; audit table; staff scoped to their own clinic(s).
- Clinic opening-hours templates and public holidays when generating slots.
