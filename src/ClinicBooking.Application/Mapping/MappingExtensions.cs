using ClinicBooking.Contracts.Appointments;
using ClinicBooking.Contracts.Clinics;
using ClinicBooking.Contracts.Slots;
using ClinicBooking.Domain.Entities;

namespace ClinicBooking.Application.Mapping;

/// <summary>
/// Entity-to-DTO mapping. Hand-written rather than AutoMapper: the mappings are small, compile-time checked
/// and easy to debug, and AutoMapper 15+ needs a commercial licence (see README).
/// </summary>
public static class MappingExtensions
{
    public static ClinicDto ToDto(this Clinic clinic) =>
        new(clinic.Id, clinic.Name, clinic.Address, clinic.District, clinic.PhoneNumber);

    public static TimeSlotDto ToDto(this TimeSlot slot) =>
        new(slot.Id, slot.ClinicId, slot.StartTime, slot.EndTime, slot.Capacity, slot.RemainingCapacity);

    /// <param name="appointment">Must be loaded with its time slot, clinic and patient.</param>
    /// <param name="now">Clinic-local time, used to work out whether the appointment can still be changed.</param>
    public static AppointmentDto ToDto(this Appointment appointment, DateTime now)
    {
        var slot = appointment.TimeSlot ?? throw new InvalidOperationException("Appointment.TimeSlot must be loaded.");
        var clinic = slot.Clinic ?? throw new InvalidOperationException("TimeSlot.Clinic must be loaded.");
        var patient = appointment.Patient ?? throw new InvalidOperationException("Appointment.Patient must be loaded.");

        return new AppointmentDto(
            appointment.Id,
            appointment.ReferenceNumber,
            clinic.Id,
            clinic.Name,
            clinic.Address,
            slot.Id,
            slot.StartTime,
            slot.EndTime,
            appointment.Status.ToString(),
            appointment.Reason,
            patient.FullName,
            patient.PhoneNumber,
            appointment.CreatedAtUtc,
            CanModify: appointment.IsActive && !slot.HasStarted(now));
    }
}
