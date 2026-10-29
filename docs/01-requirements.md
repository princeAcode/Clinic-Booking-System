# 1. Requirements Analysis

## 1.1 Purpose and scope

A simplified **Clinic Booking System** for PHCIS (Primary Health Care Information System). Patients can find a
clinic, see available time slots, book an appointment and receive a confirmation. They can also cancel or
reschedule. Clinic staff manage availability and see each day's appointment list.

**Out of scope for this version:** clinical records, referrals, SMS/email delivery (see enhancements), queue
management on the day, and multi-language UI.

## 1.2 Stakeholders

| Stakeholder | Interest / goals | How the system serves them |
|---|---|---|
| **Patients** | Book a convenient time without queuing; know their booking is secure; change plans easily | Self-service booking, instant reference number, cancel and reschedule online |
| **Clinic staff** (receptionists, nurses) | Predictable patient flow; know who is coming; manage capacity | Daily schedule per clinic, slot generation with capacity per slot, staff-initiated cancellation |
| **Clinic / facility managers** | Use consulting capacity well; reduce no-shows and overcrowding | Capacity per slot, cancellations release capacity immediately |
| **System administrators** (PHCIS IT) | Secure, maintainable, observable system; manage clinics and access | Role-based access, clinic administration API, structured logs, health endpoint, Docker deployment |
| **Department of Health / PHCIS programme** | Compliance (POPIA), equitable access, reporting | Minimal personal data, server-side authorisation, audit-friendly logs, district on each clinic |
| **Developers / support team** | Easy to change and support | Layered architecture, tests, API documentation (Swagger), CI pipeline |

## 1.3 Functional requirements

Priority uses MoSCoW: **M**ust, **S**hould, **C**ould. ✅ = implemented in this submission.

| ID | Requirement | Priority | Status |
|---|---|---|---|
| FR-01 | A patient can register with name, SA ID/passport number, date of birth, mobile number, email and password | M | ✅ |
| FR-02 | Users sign in with email and password; the system issues a time-limited token | M | ✅ |
| FR-03 | Anyone can list clinics and search them by name, address or district | M | ✅ |
| FR-04 | Anyone can view a clinic's **available** time slots for a chosen day (future slots with remaining capacity only) | M | ✅ |
| FR-05 | A signed-in patient can **book** an available slot, optionally giving a reason for the visit | M | ✅ |
| FR-06 | On booking, the patient receives a **confirmation** with a unique reference number, clinic, date and time | M | ✅ |
| FR-07 | The system **prevents double booking**: a slot can never exceed its capacity, even with simultaneous requests | M | ✅ |
| FR-08 | A patient cannot hold two active appointments at overlapping times | M | ✅ |
| FR-09 | Slots in the past, or that have already started, cannot be booked | M | ✅ |
| FR-10 | A patient can view their upcoming and past/cancelled appointments | M | ✅ |
| FR-11 | A patient can **cancel** a future appointment; the place is released immediately | M | ✅ |
| FR-12 | A patient can **reschedule** a future appointment to another available slot, keeping the reference number | M | ✅ |
| FR-13 | Patients can only see and change their own appointments | M | ✅ |
| FR-14 | Staff can view a clinic's appointment list for a day, with patient contact details | S | ✅ |
| FR-15 | Staff can open slots for a date range (opening hours, slot length, patients per slot, weekends on/off), without overlapping existing slots | S | ✅ |
| FR-16 | Staff can cancel any appointment (e.g. clinic closure) | S | ✅ |
| FR-17 | Administrators can add clinics | S | ✅ (API) |
| FR-18 | Appointment reminders by SMS/email | C | Future: Twilio/SendGrid |
| FR-19 | Real-time slot updates for users viewing the same clinic | C | Future: SignalR |
| FR-20 | Calendar sync (Outlook / Google) | C | Future |
| FR-21 | Staff mark appointments as completed / no-show | C | Future (statuses already modelled) |

## 1.4 Non-functional requirements

| ID | Category | Requirement | How it is addressed |
|---|---|---|---|
| NFR-01 | **Security: authentication** | Only registered users can book; tokens expire | ASP.NET Identity (hashed passwords), JWT bearer tokens (HMAC-SHA256, 2 h expiry) |
| NFR-02 | **Security: authorisation** | Role-based access (Patient, Staff, Admin); patients only reach their own data | `[Authorize(Roles=…)]` on endpoints, ownership checks in the application service; other patients' appointments return 404 so IDs cannot be probed |
| NFR-03 | **Security: abuse** | Slow down password guessing | Rate limit of 10 sign-in/registration attempts per minute per IP; same error for unknown email and wrong password |
| NFR-04 | **Privacy (POPIA)** | Collect only what is needed; no sensitive data in URLs or logs | Minimal patient fields, IDs not logged, reason field is optional with guidance, no PII in query strings |
| NFR-05 | **Data integrity** | No overbooking under concurrent load | Optimistic concurrency token on `TimeSlot.BookedCount`, DB check constraint `BookedCount <= Capacity`, filtered unique index on (patient, slot) for active bookings |
| NFR-06 | **Performance** | Slot lookup and booking respond in < 500 ms at p95 for normal load | Indexed queries (`ClinicId, StartTime`), `AsNoTracking` reads, WASM client keeps UI work on the device |
| NFR-07 | **Scalability** | Grow from one district to province-wide | Stateless API (JWT, no server session) scales horizontally behind a load balancer; PostgreSQL can be scaled up or read-replicated |
| NFR-08 | **Availability / operability** | Detect failures quickly | `/health` endpoint (includes DB check), structured logs (Serilog, console + rolling files), containerised deployment |
| NFR-09 | **Usability & accessibility** | Usable on low-end phones; clear errors | Responsive MudBlazor layout, tables collapse on small screens, plain-language validation messages, keyboard-accessible controls |
| NFR-10 | **Maintainability** | Easy to change safely | Clean/layered architecture, SOLID, shared validation rules between client and server, unit and component tests, CI |
| NFR-11 | **Consistency of time** | Times shown exactly as the clinic sees them | Slots stored as clinic-local time (`Africa/Johannesburg`, configurable); "past" checks use a clinic clock |
| NFR-12 | **Auditability** | Trace who did what | Structured log events for booking, cancellation, rescheduling and slot generation (with IDs, not personal data) |

## 1.5 Assumptions

1. All clinics are in one time zone (South Africa, UTC+2, no daylight saving). The zone is configurable.
2. A slot can hold more than one patient (`Capacity`), e.g. several nurses consulting at once. The default is 1.
3. Patients identify with an SA ID number (Luhn-validated) or a passport number.
4. Rescheduling is offered within the same clinic in the UI; the API allows any clinic.
5. Confirmation is shown on screen (and is printable). SMS/email delivery is a listed enhancement.
