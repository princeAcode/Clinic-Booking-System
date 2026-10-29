using ClinicBooking.Domain.Entities;

namespace ClinicBooking.UnitTests;

/// <summary>Builders for valid domain objects, so each test only spells out what matters to it.</summary>
internal static class TestData
{
    public static Clinic Clinic() => new()
    {
        Name = "Riverside Community Clinic",
        Address = "12 Main Road",
        District = "Central District",
        PhoneNumber = "0115550101",
    };

    public static Patient Patient() => new()
    {
        UserId = Guid.NewGuid().ToString(),
        FirstName = "Thandi",
        LastName = "Mokoena",
        IdNumber = "9001014800089",
        DateOfBirth = new DateOnly(1990, 1, 1),
        PhoneNumber = "0821234567",
        Email = "thandi@example.com",
    };

    public static TimeSlot Slot(Clinic clinic, DateTime start, int capacity = 1, int booked = 0) => new()
    {
        ClinicId = clinic.Id,
        Clinic = clinic,
        StartTime = start,
        EndTime = start.AddMinutes(30),
        Capacity = capacity,
        BookedCount = booked,
    };
}
