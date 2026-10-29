# 2. System Design

## 2.1 Architecture

The solution uses a **layered (Clean Architecture) monolith**. For one bounded context (booking) this gives clear
separation and testability without the operational cost of microservices. Each layer only depends inwards.
Moving to services later (for example a separate notifications service) would not require rewriting the core.

```mermaid
flowchart LR
    subgraph Browser
        C["ClinicBooking.Client<br/>Blazor WebAssembly + MudBlazor<br/>(pages, API clients, JWT auth state)"]
    end

    subgraph Server["ASP.NET Core host"]
        A["ClinicBooking.Api<br/>Controllers, JWT auth, rate limiting,<br/>problem-details errors, Serilog, Swagger"]
        AP["ClinicBooking.Application<br/>Use-case services, booking rules,<br/>repository interfaces, mapping"]
        D["ClinicBooking.Domain<br/>Entities + invariants<br/>(Clinic, Patient, TimeSlot, Appointment)"]
        I["ClinicBooking.Infrastructure<br/>EF Core + Npgsql, Identity,<br/>JWT issuing, clock, seeding"]
    end

    K["ClinicBooking.Contracts<br/>DTOs + FluentValidation rules<br/>(shared by client and server)"]
    DB[("PostgreSQL")]

    C -- "HTTPS / JSON + Bearer JWT" --> A
    A --> AP
    AP --> D
    I -. implements interfaces .-> AP
    I --> DB
    C -.-> K
    AP -.-> K
```

| Project | Responsibility | Depends on |
|---|---|---|
| `Domain` | Entities and rules that must always hold (capacity, state changes) | nothing |
| `Contracts` | Request/response DTOs and validators shared with the Blazor client | FluentValidation |
| `Application` | Use cases (book, cancel, reschedule, list, generate slots); defines `I…Repository`, `IUnitOfWork`, `ICurrentUser`, `IClinicClock` | Domain, Contracts |
| `Infrastructure` | EF Core `AppDbContext`, repositories, migrations, ASP.NET Identity, JWT generation, seed data | Application |
| `Api` | HTTP endpoints, authentication/authorisation, error mapping, logging, hosts the WASM client | Application, Infrastructure |
| `Client` | Blazor WebAssembly UI | Contracts |

**SOLID in practice**

- **S**: each service covers one area (`AppointmentService`, `TimeSlotService`, `ClinicService`); `SlotGenerator` is a pure function; controllers only translate HTTP.
- **O**: new rules or storage can be added by adding implementations. For example, an SMS notifier would be another `INotificationSender` without changing the booking code.
- **L**: repository implementations follow their interface contracts, so the unit tests use mocks in their place.
- **I**: small focused interfaces (`IClinicClock`, `IReferenceNumberGenerator`, `ICurrentUser`) rather than one large service.
- **D**: the application layer depends on abstractions; EF Core, Identity and the system clock are injected from Infrastructure.

## 2.2 Data model

```mermaid
erDiagram
    AspNetUsers ||--o| Patient : "has profile"
    Patient ||--o{ Appointment : books
    Clinic ||--o{ TimeSlot : offers
    TimeSlot ||--o{ Appointment : "is booked by"

    Clinic {
        uuid Id PK
        varchar(150) Name UK
        varchar(300) Address
        varchar(100) District
        varchar(20) PhoneNumber
    }
    Patient {
        uuid Id PK
        text UserId FK,UK "Identity user"
        varchar(100) FirstName
        varchar(100) LastName
        varchar(20) IdNumber UK "SA ID or passport"
        date DateOfBirth
        varchar(20) PhoneNumber
        varchar(256) Email
    }
    TimeSlot {
        uuid Id PK
        uuid ClinicId FK
        timestamp StartTime "clinic-local"
        timestamp EndTime "clinic-local, > StartTime"
        int Capacity "patients per slot"
        int BookedCount "concurrency token, 0..Capacity"
    }
    Appointment {
        uuid Id PK
        varchar(20) ReferenceNumber UK "PHC-XXXXXXXX"
        uuid PatientId FK
        uuid TimeSlotId FK
        varchar(500) Reason "optional"
        varchar(20) Status "Booked|Cancelled|Completed|NoShow"
        timestamptz CreatedAtUtc
        timestamptz CancelledAtUtc
    }
```

**Key design decisions**

| Decision | Reason |
|---|---|
| `TimeSlot.Capacity` + `BookedCount` instead of "one appointment per slot" | Clinics often run several consulting rooms in parallel. Availability is a single indexed comparison (`BookedCount < Capacity`). |
| `BookedCount` is an **optimistic concurrency token** | Two patients racing for the last place: both read `BookedCount = 0`; the first `UPDATE … WHERE BookedCount = 0` succeeds; the second affects 0 rows → `DbUpdateConcurrencyException` → HTTP 409. No locks are held. |
| Check constraint `BookedCount BETWEEN 0 AND Capacity` | Last line of defence at the database level. |
| Filtered unique index `(PatientId, TimeSlotId) WHERE Status = 'Booked'` | A patient can never hold the same slot twice. Cancelled history is kept. |
| Slot times stored **without time zone** (clinic-local) | Clinics think in local wall-clock time; this avoids off-by-two-hours bugs in display. "Now" comes from `IClinicClock` in the configured zone. Audit timestamps (`CreatedAtUtc`) are stored in UTC. |
| Appointment status kept (soft cancel) instead of deleting | History and reporting (no-shows, cancellations); auditability. |
| `Patient` separate from the Identity user | Keeps clinical/demographic data out of the auth tables; staff accounts have no patient record. |
| GUID keys | Not guessable in URLs; can be generated by the application before saving. |

## 2.3 Booking flow (sequence)

```mermaid
sequenceDiagram
    autonumber
    actor P as Patient
    participant UI as Blazor client
    participant API as AppointmentsController
    participant S as AppointmentService
    participant R as Repositories
    participant DB as PostgreSQL

    P->>UI: Choose clinic and date
    UI->>API: GET /api/clinics/{id}/slots?date=
    API->>S: (TimeSlotService) GetAvailableSlots
    S->>R: slots where start >= now and BookedCount < Capacity
    R->>DB: SELECT …
    DB-->>UI: available slots
    P->>UI: Pick slot, enter reason, Confirm
    UI->>UI: FluentValidation (same rules as server)
    UI->>API: POST /api/appointments {timeSlotId, reason} + Bearer JWT
    API->>API: Validate JWT, require role Patient
    API->>S: BookAsync(request)
    S->>S: Validate request
    S->>R: Get slot (+clinic), patient
    alt slot started
        S-->>UI: 422 "slot is in the past"
    else slot full
        S-->>UI: 409 "fully booked"
    else patient already booked at that time
        S-->>UI: 409 "already have an appointment"
    else OK
        S->>S: Appointment.Book() → slot.Reserve() (BookedCount+1)
        S->>R: Add appointment, SaveChanges
        R->>DB: INSERT appointment, UPDATE slot WHERE BookedCount = old
        alt concurrent booking won the race
            DB-->>S: 0 rows → concurrency exception
            S-->>UI: 409 "someone else just booked this slot"
        else success
            DB-->>S: committed
            S-->>API: BookingConfirmationDto (reference, message)
            API-->>UI: 201 Created
            UI->>P: Confirmation page with reference PHC-XXXXXXXX
        end
    end
```

## 2.4 Validation and error handling flow

```mermaid
flowchart TD
    A[Booking request] --> B{Field validation<br/>client + server<br/>FluentValidation}
    B -- invalid --> E400[400 Validation problem<br/>shown next to fields]
    B -- valid --> C{Signed in as patient?}
    C -- no --> E401[401 / 403]
    C -- yes --> D{Slot exists?}
    D -- no --> E404[404 Not found]
    D -- yes --> F{Slot start in future?}
    F -- no --> E422[422 Business rule]
    F -- yes --> G{Remaining capacity?}
    G -- no --> E409a[409 Fully booked]
    G -- yes --> H{Patient has overlapping<br/>active appointment?}
    H -- yes --> E409b[409 Already booked at this time]
    H -- no --> I[Reserve place + save]
    I --> J{Concurrency check<br/>BookedCount unchanged?}
    J -- no --> E409c[409 Someone else just booked]
    J -- yes --> K[201 Confirmation<br/>reference number + message]
```

All errors are returned as RFC 7807 **problem details** by a single `GlobalExceptionHandler`, so the client
shows one consistent, user-friendly message. Unexpected errors are logged with their stack trace and return a
generic 500 that does not expose internals.

## 2.5 Appointment lifecycle

```mermaid
stateDiagram-v2
    [*] --> Booked: Book (reserve place)
    Booked --> Booked: Reschedule (reserve new, release old)
    Booked --> Cancelled: Cancel (release place)
    Booked --> Completed: Attended (future)
    Booked --> NoShow: Did not attend (future)
    Cancelled --> [*]
    Completed --> [*]
    NoShow --> [*]
```

## 2.6 API surface

| Method | Route | Who | Purpose |
|---|---|---|---|
| POST | `/api/auth/register` | anyone | Create a patient account, returns JWT |
| POST | `/api/auth/login` | anyone | Sign in, returns JWT |
| GET | `/api/clinics` | anyone | List clinics |
| GET | `/api/clinics/{id}` | anyone | Clinic details |
| POST | `/api/clinics` | Admin | Add a clinic |
| GET | `/api/clinics/{id}/slots?date=yyyy-MM-dd` | anyone | Available slots on a day |
| POST | `/api/clinics/{id}/slots/generate` | Staff, Admin | Open slots for a date range |
| GET | `/api/clinics/{id}/appointments?date=` | Staff, Admin | Day's appointment list |
| POST | `/api/appointments` | Patient | Book → confirmation |
| GET | `/api/appointments/mine` | Patient | Own appointments |
| GET | `/api/appointments/{id}` | owner, Staff | One appointment |
| POST | `/api/appointments/{id}/cancel` | owner, Staff | Cancel |
| POST | `/api/appointments/{id}/reschedule` | owner, Staff | Move to another slot |
| GET | `/health` | anyone | Liveness + database check |

Full request/response schemas are in Swagger at `/swagger` when running in Development.
