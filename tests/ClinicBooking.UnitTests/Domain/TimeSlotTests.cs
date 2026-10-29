using ClinicBooking.Domain.Entities;
using ClinicBooking.Domain.Exceptions;

namespace ClinicBooking.UnitTests.Domain;

public sealed class TimeSlotTests
{
    private static readonly DateTime Start = new(2026, 11, 2, 9, 0, 0);

    [Fact]
    public void Reserve_WithRoom_IncrementsBookedCount()
    {
        var slot = TestData.Slot(TestData.Clinic(), Start, capacity: 2);

        slot.Reserve();

        Assert.Equal(1, slot.BookedCount);
        Assert.Equal(1, slot.RemainingCapacity);
        Assert.True(slot.HasAvailability);
    }

    [Fact]
    public void Reserve_WhenFull_ThrowsAndDoesNotOverbook()
    {
        var slot = TestData.Slot(TestData.Clinic(), Start, capacity: 1);
        slot.Reserve();

        Assert.Throws<BookingConflictException>(slot.Reserve);
        Assert.Equal(1, slot.BookedCount);
    }

    [Fact]
    public void Release_NeverGoesBelowZero()
    {
        var slot = TestData.Slot(TestData.Clinic(), Start);

        slot.Release();

        Assert.Equal(0, slot.BookedCount);
    }

    [Theory]
    [InlineData(8, 30, 9, 0, false)]   // ends exactly when the slot starts
    [InlineData(9, 30, 10, 0, false)]  // starts exactly when the slot ends
    [InlineData(8, 45, 9, 15, true)]   // overlaps the start
    [InlineData(9, 10, 9, 20, true)]   // inside the slot
    [InlineData(8, 0, 10, 0, true)]    // covers the slot
    public void Overlaps_DetectsSharedTimeOnly(int startHour, int startMinute, int endHour, int endMinute, bool expected)
    {
        var slot = TestData.Slot(TestData.Clinic(), Start); // 09:00–09:30

        var overlaps = slot.Overlaps(Start.Date.Add(new TimeSpan(startHour, startMinute, 0)), Start.Date.Add(new TimeSpan(endHour, endMinute, 0)));

        Assert.Equal(expected, overlaps);
    }
}

public sealed class AppointmentTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 8, 0, 0);

    [Fact]
    public void Book_BlankReason_IsStoredAsNull()
    {
        var slot = TestData.Slot(TestData.Clinic(), Now.AddHours(1));

        var appointment = Appointment.Book(TestData.Patient(), slot, "PHC-1", "   ", Now);

        Assert.Null(appointment.Reason);
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
    }

    [Fact]
    public void MoveTo_FullSlot_LeavesOriginalBookingUntouched()
    {
        var clinic = TestData.Clinic();
        var original = TestData.Slot(clinic, Now.AddHours(1));
        var full = TestData.Slot(clinic, Now.AddHours(2), capacity: 1, booked: 1);
        var appointment = Appointment.Book(TestData.Patient(), original, "PHC-1", null, Now);

        Assert.Throws<BookingConflictException>(() => appointment.MoveTo(full));

        Assert.Equal(original.Id, appointment.TimeSlotId);
        Assert.Equal(1, original.BookedCount);
        Assert.Equal(1, full.BookedCount);
    }

    [Fact]
    public void Cancel_Twice_Throws()
    {
        var appointment = Appointment.Book(TestData.Patient(), TestData.Slot(TestData.Clinic(), Now.AddHours(1)), "PHC-1", null, Now);
        appointment.Cancel(Now);

        Assert.Throws<BusinessRuleException>(() => appointment.Cancel(Now));
    }
}
