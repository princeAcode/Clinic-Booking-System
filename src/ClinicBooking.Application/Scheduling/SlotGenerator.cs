using ClinicBooking.Domain.Entities;

namespace ClinicBooking.Application.Scheduling;

/// <summary>Builds evenly spaced time slots for a clinic's opening hours. Pure function, no I/O.</summary>
public static class SlotGenerator
{
    /// <summary>
    /// Creates slots of <paramref name="duration"/> between <paramref name="dayStart"/> and <paramref name="dayEnd"/>
    /// for every day from <paramref name="fromDate"/> to <paramref name="toDate"/> inclusive.
    /// A final slot that would run past closing time is not created.
    /// </summary>
    public static IReadOnlyList<TimeSlot> Generate(
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        TimeOnly dayStart,
        TimeOnly dayEnd,
        TimeSpan duration,
        int capacity,
        bool includeWeekends)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        var slots = new List<TimeSlot>();

        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            if (!includeWeekends && date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var start = date.ToDateTime(dayStart);
            var closing = date.ToDateTime(dayEnd);

            while (start + duration <= closing)
            {
                slots.Add(new TimeSlot
                {
                    ClinicId = clinicId,
                    StartTime = start,
                    EndTime = start + duration,
                    Capacity = capacity,
                });
                start += duration;
            }
        }

        return slots;
    }
}
