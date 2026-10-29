using ClinicBooking.Contracts.Appointments;

namespace ClinicBooking.Client.Services;

/// <summary>
/// Hands the confirmation returned by a booking or reschedule to the confirmation page,
/// so the page can show the server's confirmation message without another request.
/// </summary>
public sealed class BookingConfirmationStore
{
    private BookingConfirmationDto? _last;

    public void Set(BookingConfirmationDto confirmation) => _last = confirmation;

    /// <summary>Returns the stored confirmation if it is for this appointment, and clears it.</summary>
    public BookingConfirmationDto? Take(Guid appointmentId)
    {
        var confirmation = _last?.Appointment.Id == appointmentId ? _last : null;
        _last = null;
        return confirmation;
    }
}
