using ClinicBooking.Domain.Entities;

namespace ClinicBooking.Application.Abstractions;

// Repository interfaces keep the application layer independent of EF Core (Dependency Inversion)
// and make the booking rules easy to unit test with mocks.

public interface IClinicRepository
{
    Task<IReadOnlyList<Clinic>> GetAllAsync(CancellationToken ct = default);
    Task<Clinic?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    void Add(Clinic clinic);
}

public interface ITimeSlotRepository
{
    /// <summary>Gets a slot including its clinic.</summary>
    Task<TimeSlot?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Slots at a clinic that start within [from, to) and still have room.</summary>
    Task<IReadOnlyList<TimeSlot>> GetAvailableAsync(Guid clinicId, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>True when any existing slot at the clinic overlaps [from, to).</summary>
    Task<bool> AnyOverlappingAsync(Guid clinicId, DateTime from, DateTime to, CancellationToken ct = default);

    void AddRange(IEnumerable<TimeSlot> slots);
}

public interface IAppointmentRepository
{
    /// <summary>Gets an appointment including its slot, clinic and patient.</summary>
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Appointment>> GetForPatientAsync(Guid patientId, CancellationToken ct = default);

    Task<IReadOnlyList<Appointment>> GetForClinicAsync(Guid clinicId, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// True when the patient already has an active appointment overlapping [start, end),
    /// optionally ignoring one appointment (used when rescheduling).
    /// </summary>
    Task<bool> PatientHasOverlappingAsync(Guid patientId, DateTime start, DateTime end, Guid? excludeAppointmentId = null, CancellationToken ct = default);

    void Add(Appointment appointment);
}

public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> IdNumberExistsAsync(string idNumber, CancellationToken ct = default);
    void Add(Patient patient);
}

/// <summary>Commits all changes made through the repositories as one transaction.</summary>
public interface IUnitOfWork
{
    /// <exception cref="Domain.Exceptions.BookingConflictException">
    /// Another request changed the same slot at the same time (optimistic concurrency).
    /// </exception>
    Task SaveChangesAsync(CancellationToken ct = default);
}
