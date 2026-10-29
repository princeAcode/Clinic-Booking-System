using ClinicBooking.Application.Abstractions;
using ClinicBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Infrastructure.Persistence;

internal sealed class ClinicRepository(AppDbContext db) : IClinicRepository
{
    public async Task<IReadOnlyList<Clinic>> GetAllAsync(CancellationToken ct = default) =>
        await db.Clinics.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);

    public Task<Clinic?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        db.Clinics.AnyAsync(c => c.Id == id, ct);

    public void Add(Clinic clinic) => db.Clinics.Add(clinic);
}

internal sealed class TimeSlotRepository(AppDbContext db) : ITimeSlotRepository
{
    public Task<TimeSlot?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.TimeSlots.Include(s => s.Clinic).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<TimeSlot>> GetAvailableAsync(Guid clinicId, DateTime from, DateTime to, CancellationToken ct = default) =>
        await db.TimeSlots.AsNoTracking()
            .Where(s => s.ClinicId == clinicId && s.StartTime >= from && s.StartTime < to && s.BookedCount < s.Capacity)
            .OrderBy(s => s.StartTime)
            .ToListAsync(ct);

    public Task<bool> AnyOverlappingAsync(Guid clinicId, DateTime from, DateTime to, CancellationToken ct = default) =>
        db.TimeSlots.AnyAsync(s => s.ClinicId == clinicId && s.StartTime < to && from < s.EndTime, ct);

    public void AddRange(IEnumerable<TimeSlot> slots) => db.TimeSlots.AddRange(slots);
}

internal sealed class AppointmentRepository(AppDbContext db) : IAppointmentRepository
{
    private IQueryable<Appointment> WithDetails() =>
        db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.TimeSlot).ThenInclude(s => s!.Clinic);

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Appointment>> GetForPatientAsync(Guid patientId, CancellationToken ct = default) =>
        await WithDetails().AsNoTracking()
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.TimeSlot!.StartTime)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> GetForClinicAsync(Guid clinicId, DateTime from, DateTime to, CancellationToken ct = default) =>
        await WithDetails().AsNoTracking()
            .Where(a => a.TimeSlot!.ClinicId == clinicId && a.TimeSlot.StartTime >= from && a.TimeSlot.StartTime < to)
            .OrderBy(a => a.TimeSlot!.StartTime)
            .ThenBy(a => a.Patient!.LastName)
            .ToListAsync(ct);

    public Task<bool> PatientHasOverlappingAsync(Guid patientId, DateTime start, DateTime end, Guid? excludeAppointmentId = null, CancellationToken ct = default) =>
        db.Appointments.AnyAsync(a =>
            a.PatientId == patientId &&
            a.Status == AppointmentStatus.Booked &&
            (excludeAppointmentId == null || a.Id != excludeAppointmentId) &&
            a.TimeSlot!.StartTime < end &&
            start < a.TimeSlot.EndTime, ct);

    public void Add(Appointment appointment) => db.Appointments.Add(appointment);
}

internal sealed class PatientRepository(AppDbContext db) : IPatientRepository
{
    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Patients.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> IdNumberExistsAsync(string idNumber, CancellationToken ct = default) =>
        db.Patients.AnyAsync(p => p.IdNumber == idNumber, ct);

    public void Add(Patient patient) => db.Patients.Add(patient);
}
