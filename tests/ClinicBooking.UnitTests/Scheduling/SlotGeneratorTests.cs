using ClinicBooking.Application.Scheduling;

namespace ClinicBooking.UnitTests.Scheduling;

public sealed class SlotGeneratorTests
{
    private static readonly Guid ClinicId = Guid.NewGuid();
    private static readonly DateOnly Monday = new(2026, 11, 2);

    [Fact]
    public void Generate_OneDay_CreatesBackToBackSlotsWithinOpeningHours()
    {
        var slots = SlotGenerator.Generate(ClinicId, Monday, Monday, new TimeOnly(8, 0), new TimeOnly(10, 0),
            TimeSpan.FromMinutes(30), capacity: 2, includeWeekends: false);

        Assert.Equal(4, slots.Count);
        Assert.Equal(Monday.ToDateTime(new TimeOnly(8, 0)), slots[0].StartTime);
        Assert.Equal(Monday.ToDateTime(new TimeOnly(10, 0)), slots[^1].EndTime);
        Assert.All(slots, s => Assert.Equal(2, s.Capacity));
        Assert.All(slots, s => Assert.Equal(ClinicId, s.ClinicId));
        Assert.All(slots.Zip(slots.Skip(1)), pair => Assert.Equal(pair.First.EndTime, pair.Second.StartTime));
    }

    [Fact]
    public void Generate_DurationNotDividingEvenly_DropsSlotThatWouldRunPastClosing()
    {
        var slots = SlotGenerator.Generate(ClinicId, Monday, Monday, new TimeOnly(8, 0), new TimeOnly(9, 0),
            TimeSpan.FromMinutes(25), capacity: 1, includeWeekends: false);

        Assert.Equal(2, slots.Count);
        Assert.True(slots[^1].EndTime <= Monday.ToDateTime(new TimeOnly(9, 0)));
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 7)]
    public void Generate_FullWeek_RespectsWeekendSetting(bool includeWeekends, int expectedDays)
    {
        var slots = SlotGenerator.Generate(ClinicId, Monday, Monday.AddDays(6), new TimeOnly(8, 0), new TimeOnly(9, 0),
            TimeSpan.FromMinutes(60), capacity: 1, includeWeekends);

        Assert.Equal(expectedDays, slots.Count);
    }
}
