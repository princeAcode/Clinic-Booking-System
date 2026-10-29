using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Exceptions;
using ClinicBooking.Application.Mapping;
using ClinicBooking.Contracts.Appointments;
using ClinicBooking.Domain.Entities;
using ClinicBooking.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ClinicBooking.Application.Services;

public interface IAppointmentService
{
    Task<BookingConfirmationDto> BookAsync(BookAppointmentRequest request, CancellationToken ct = default);
    Task<AppointmentDto> CancelAsync(Guid appointmentId, CancellationToken ct = default);
    Task<BookingConfirmationDto> RescheduleAsync(Guid appointmentId, RescheduleAppointmentRequest request, CancellationToken ct = default);
    Task<AppointmentDto> GetAsync(Guid appointmentId, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentDto>> GetMyAppointmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentDto>> GetClinicAppointmentsAsync(Guid clinicId, DateOnly date, CancellationToken ct = default);
}

/// <summary>
/// Booking, cancellation and rescheduling. All booking rules are enforced here (and in the domain entities),
/// so they apply no matter which client calls the API.
/// </summary>
/// <remarks>
/// Rules:
/// <list type="bullet">
/// <item>Only patients book; patients can only see and change their own appointments.</item>
/// <item>Slots that have started cannot be booked, cancelled or moved.</item>
/// <item>A slot cannot be booked beyond its capacity (no double booking).</item>
/// <item>A patient cannot hold two active appointments at overlapping times.</item>
/// </list>
/// </remarks>
public sealed class AppointmentService(
    IAppointmentRepository appointments,
    ITimeSlotRepository slots,
    IPatientRepository patients,
    IClinicRepository clinics,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClinicClock clock,
    IReferenceNumberGenerator referenceNumbers,
    IValidator<BookAppointmentRequest> bookValidator,
    IValidator<RescheduleAppointmentRequest> rescheduleValidator,
    ILogger<AppointmentService> logger) : IAppointmentService
{
    public async Task<BookingConfirmationDto> BookAsync(BookAppointmentRequest request, CancellationToken ct = default)
    {
        await bookValidator.ValidateAndThrowAsync(request, ct);

        var patientId = RequirePatient();
        var patient = await patients.GetByIdAsync(patientId, ct) ?? throw new NotFoundException("Patient", patientId);
        var slot = await GetBookableSlotAsync(request.TimeSlotId, ct);

        await EnsureNoOverlapAsync(patientId, slot, excludeAppointmentId: null, ct);

        var appointment = Appointment.Book(patient, slot, referenceNumbers.Generate(), request.Reason, clock.UtcNow);
        appointments.Add(appointment);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Appointment {ReferenceNumber} booked for patient {PatientId} in slot {SlotId}",
            appointment.ReferenceNumber, patientId, slot.Id);

        return new BookingConfirmationDto(
            appointment.ToDto(clock.Now),
            BuildConfirmationMessage("Your appointment is confirmed", appointment, slot));
    }

    public async Task<AppointmentDto> CancelAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var appointment = await GetAccessibleAppointmentAsync(appointmentId, ct);
        EnsureNotStarted(appointment.TimeSlot!, "cancelled");

        appointment.Cancel(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Appointment {ReferenceNumber} cancelled", appointment.ReferenceNumber);

        return appointment.ToDto(clock.Now);
    }

    public async Task<BookingConfirmationDto> RescheduleAsync(Guid appointmentId, RescheduleAppointmentRequest request, CancellationToken ct = default)
    {
        await rescheduleValidator.ValidateAndThrowAsync(request, ct);

        var appointment = await GetAccessibleAppointmentAsync(appointmentId, ct);
        EnsureNotStarted(appointment.TimeSlot!, "rescheduled");

        var newSlot = await GetBookableSlotAsync(request.NewTimeSlotId, ct);
        await EnsureNoOverlapAsync(appointment.PatientId, newSlot, excludeAppointmentId: appointment.Id, ct);

        var previousSlotId = appointment.TimeSlotId;
        appointment.MoveTo(newSlot);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Appointment {ReferenceNumber} moved from slot {OldSlotId} to {NewSlotId}",
            appointment.ReferenceNumber, previousSlotId, newSlot.Id);

        return new BookingConfirmationDto(
            appointment.ToDto(clock.Now),
            BuildConfirmationMessage("Your appointment has been rescheduled", appointment, newSlot));
    }

    public async Task<AppointmentDto> GetAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var appointment = await GetAccessibleAppointmentAsync(appointmentId, ct);
        return appointment.ToDto(clock.Now);
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetMyAppointmentsAsync(CancellationToken ct = default)
    {
        var patientId = RequirePatient();
        var mine = await appointments.GetForPatientAsync(patientId, ct);
        var now = clock.Now;
        return mine.Select(a => a.ToDto(now)).ToList();
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetClinicAppointmentsAsync(Guid clinicId, DateOnly date, CancellationToken ct = default)
    {
        if (!currentUser.IsStaff)
        {
            throw new ForbiddenAccessException("Only clinic staff can view a clinic's appointment list.");
        }

        if (!await clinics.ExistsAsync(clinicId, ct))
        {
            throw new NotFoundException("Clinic", clinicId);
        }

        var from = date.ToDateTime(TimeOnly.MinValue);
        var list = await appointments.GetForClinicAsync(clinicId, from, from.AddDays(1), ct);
        var now = clock.Now;
        return list.Select(a => a.ToDto(now)).ToList();
    }

    private Guid RequirePatient() =>
        currentUser.PatientId ?? throw new ForbiddenAccessException("Only patients can book appointments.");

    /// <summary>Loads a slot and checks it is in the future and has room.</summary>
    private async Task<TimeSlot> GetBookableSlotAsync(Guid slotId, CancellationToken ct)
    {
        var slot = await slots.GetByIdAsync(slotId, ct) ?? throw new NotFoundException("Time slot", slotId);

        if (slot.HasStarted(clock.Now))
        {
            throw new BusinessRuleException("This time slot is in the past. Please choose a future time.");
        }

        if (!slot.HasAvailability)
        {
            throw new BookingConflictException("This time slot is fully booked. Please choose another time.");
        }

        return slot;
    }

    private async Task EnsureNoOverlapAsync(Guid patientId, TimeSlot slot, Guid? excludeAppointmentId, CancellationToken ct)
    {
        if (await appointments.PatientHasOverlappingAsync(patientId, slot.StartTime, slot.EndTime, excludeAppointmentId, ct))
        {
            throw new BookingConflictException("You already have an appointment at this time.");
        }
    }

    /// <summary>Loads an appointment the current user may see: their own, or any appointment for staff.</summary>
    private async Task<Appointment> GetAccessibleAppointmentAsync(Guid appointmentId, CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(appointmentId, ct)
            ?? throw new NotFoundException("Appointment", appointmentId);

        if (!currentUser.IsStaff && appointment.PatientId != currentUser.PatientId)
        {
            // Report "not found" rather than "forbidden" so other patients' appointment IDs cannot be probed.
            throw new NotFoundException("Appointment", appointmentId);
        }

        return appointment;
    }

    private void EnsureNotStarted(TimeSlot slot, string action)
    {
        if (slot.HasStarted(clock.Now))
        {
            throw new BusinessRuleException($"Appointments that have already started cannot be {action}.");
        }
    }

    private static string BuildConfirmationMessage(string heading, Appointment appointment, TimeSlot slot) =>
        $"{heading}. Reference {appointment.ReferenceNumber}: {slot.Clinic?.Name} on " +
        $"{slot.StartTime:dddd d MMMM yyyy} at {slot.StartTime:HH:mm}. " +
        "Please arrive 15 minutes early and bring your ID document.";
}
