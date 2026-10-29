using ClinicBooking.Domain.Exceptions;

namespace ClinicBooking.Domain.Entities;

/// <summary>
/// A patient's booking of a place in a <see cref="TimeSlot"/>.
/// State changes go through the methods on this class so the slot capacity always stays in step.
/// </summary>
public sealed class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Short human-friendly code shown on the confirmation (e.g. PHC-7K2M9QXA).</summary>
    public required string ReferenceNumber { get; set; }

    public Guid PatientId { get; set; }

    public Patient? Patient { get; set; }

    public Guid TimeSlotId { get; set; }

    public TimeSlot? TimeSlot { get; set; }

    /// <summary>Optional reason for the visit, captured to help clinic triage.</summary>
    public string? Reason { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public bool IsActive => Status == AppointmentStatus.Booked;

    /// <summary>
    /// Creates a booked appointment and reserves a place in the slot.
    /// </summary>
    /// <exception cref="BookingConflictException">The slot is full.</exception>
    public static Appointment Book(Patient patient, TimeSlot slot, string referenceNumber, string? reason, DateTime createdAtUtc)
    {
        slot.Reserve();

        return new Appointment
        {
            PatientId = patient.Id,
            Patient = patient,
            TimeSlotId = slot.Id,
            TimeSlot = slot,
            ReferenceNumber = referenceNumber,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedAtUtc = createdAtUtc,
        };
    }

    /// <summary>Cancels the appointment and frees its place in the slot.</summary>
    /// <exception cref="BusinessRuleException">The appointment is not active.</exception>
    public void Cancel(DateTime cancelledAtUtc)
    {
        EnsureActive();

        TimeSlot?.Release();
        Status = AppointmentStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
    }

    /// <summary>
    /// Moves the appointment to another slot, keeping the same reference number.
    /// The new slot is reserved before the old one is released, so a full new slot leaves everything unchanged.
    /// </summary>
    public void MoveTo(TimeSlot newSlot)
    {
        EnsureActive();

        if (newSlot.Id == TimeSlotId)
        {
            throw new BusinessRuleException("The appointment is already booked for this time slot.");
        }

        newSlot.Reserve();
        TimeSlot?.Release();

        TimeSlotId = newSlot.Id;
        TimeSlot = newSlot;
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new BusinessRuleException($"Only booked appointments can be changed. This appointment is {Status.ToString().ToLowerInvariant()}.");
        }
    }
}
