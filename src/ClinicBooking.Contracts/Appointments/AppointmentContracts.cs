using FluentValidation;

namespace ClinicBooking.Contracts.Appointments;

/// <summary>An appointment as shown to patients and staff. Times are in the clinic's local time.</summary>
public sealed record AppointmentDto(
    Guid Id,
    string ReferenceNumber,
    Guid ClinicId,
    string ClinicName,
    string ClinicAddress,
    Guid TimeSlotId,
    DateTime StartTime,
    DateTime EndTime,
    string Status,
    string? Reason,
    string PatientName,
    string PatientPhoneNumber,
    DateTime CreatedAtUtc,
    bool CanModify);

/// <summary>Returned after a successful booking or reschedule.</summary>
public sealed record BookingConfirmationDto(AppointmentDto Appointment, string Message);

public sealed class BookAppointmentRequest
{
    public Guid TimeSlotId { get; set; }
    public string? Reason { get; set; }
}

public sealed class RescheduleAppointmentRequest
{
    public Guid NewTimeSlotId { get; set; }
}

public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public const int ReasonMaxLength = 500;

    public BookAppointmentRequestValidator()
    {
        RuleFor(x => x.TimeSlotId).NotEmpty().WithMessage("Choose a time slot.");
        RuleFor(x => x.Reason).MaximumLength(ReasonMaxLength);
    }
}

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(x => x.NewTimeSlotId).NotEmpty().WithMessage("Choose a new time slot.");
    }
}
