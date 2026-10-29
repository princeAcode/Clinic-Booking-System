using ClinicBooking.Domain.Exceptions;

namespace ClinicBooking.Domain.Entities;

/// <summary>
/// A bookable period at a clinic. A slot can hold up to <see cref="Capacity"/> appointments
/// (for example, when several nurses consult in parallel).
/// </summary>
/// <remarks>
/// Times are stored in the clinic's local time. <see cref="BookedCount"/> is configured as an
/// optimistic concurrency token, so two patients racing for the last place cannot both succeed.
/// </remarks>
public sealed class TimeSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClinicId { get; set; }

    public Clinic? Clinic { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int Capacity { get; set; } = 1;

    public int BookedCount { get; set; }

    public int RemainingCapacity => Math.Max(0, Capacity - BookedCount);

    public bool HasAvailability => BookedCount < Capacity;

    /// <summary>True once the slot's start time has been reached; such slots can no longer be booked or changed.</summary>
    public bool HasStarted(DateTime now) => StartTime <= now;

    /// <summary>True when the two periods share any time (touching end/start does not count).</summary>
    public bool Overlaps(DateTime start, DateTime end) => StartTime < end && start < EndTime;

    /// <summary>Takes one place in the slot.</summary>
    /// <exception cref="BookingConflictException">The slot is already full.</exception>
    public void Reserve()
    {
        if (!HasAvailability)
        {
            throw new BookingConflictException("This time slot is fully booked. Please choose another time.");
        }

        BookedCount++;
    }

    /// <summary>Gives back one place in the slot (after a cancellation or reschedule).</summary>
    public void Release()
    {
        if (BookedCount > 0)
        {
            BookedCount--;
        }
    }
}
