namespace ClinicBooking.Domain.Entities;

/// <summary>
/// A primary health care facility where patients can book appointments.
/// </summary>
public sealed class Clinic
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public required string Address { get; set; }

    /// <summary>Health district the clinic belongs to (used for filtering and reporting).</summary>
    public required string District { get; set; }

    public required string PhoneNumber { get; set; }

    public ICollection<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();
}
