using System.Net;
using Bunit;
using ClinicBooking.Client.Components;
using ClinicBooking.Client.Services;
using ClinicBooking.Contracts.Slots;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;

namespace ClinicBooking.Client.Tests;

/// <remarks>
/// Uses composition instead of inheriting BunitContext so the context is disposed asynchronously:
/// some MudBlazor services only implement IAsyncDisposable.
/// </remarks>
public sealed class SlotPickerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private static readonly Guid ClinicId = Guid.NewGuid();
    private readonly Mock<IClinicApi> _clinicApi = new();

    public SlotPickerTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();
        _ctx.Services.AddSingleton(_clinicApi.Object);
        _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static TimeSlotDto Slot(int hour, int minute = 0, int remaining = 1)
    {
        var start = DateTime.Today.AddDays(1).AddHours(hour).AddMinutes(minute);
        return new TimeSlotDto(Guid.NewGuid(), ClinicId, start, start.AddMinutes(30), 2, remaining);
    }

    private void GivenSlots(params TimeSlotDto[] slots) =>
        _clinicApi.Setup(a => a.GetAvailableSlotsAsync(ClinicId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);

    [Fact]
    public void ShowsAvailableSlotsGroupedIntoMorningAndAfternoon()
    {
        GivenSlots(Slot(9), Slot(9, 30), Slot(14));

        var cut = _ctx.Render<SlotPicker>(p => p.Add(x => x.ClinicId, ClinicId));

        Assert.Contains("Morning", cut.Markup);
        Assert.Contains("Afternoon", cut.Markup);
        Assert.Equal(3, cut.FindAll(".slot-grid button").Count);
        Assert.Contains("09:30", cut.Markup);
    }

    [Fact]
    public void LeavesOutTheExcludedSlot()
    {
        var current = Slot(9);
        GivenSlots(current, Slot(10));

        var cut = _ctx.Render<SlotPicker>(p => p
            .Add(x => x.ClinicId, ClinicId)
            .Add(x => x.ExcludeSlotId, current.Id));

        Assert.Single(cut.FindAll(".slot-grid button"));
        Assert.DoesNotContain("09:00", cut.Markup);
    }

    [Fact]
    public void ClickingASlotRaisesOnSlotSelected()
    {
        var slot = Slot(11);
        GivenSlots(slot);
        TimeSlotDto? selected = null;

        var cut = _ctx.Render<SlotPicker>(p => p
            .Add(x => x.ClinicId, ClinicId)
            .Add(x => x.OnSlotSelected, s => selected = s));
        cut.Find(".slot-grid button").Click();

        Assert.Equal(slot.Id, selected?.Id);
    }

    [Fact]
    public void NoSlots_ShowsTryAnotherDateMessage()
    {
        GivenSlots();

        var cut = _ctx.Render<SlotPicker>(p => p.Add(x => x.ClinicId, ClinicId));

        Assert.Contains("No available times on this day", cut.Markup);
    }

    [Fact]
    public void ApiError_ShowsTheErrorMessage()
    {
        _clinicApi.Setup(a => a.GetAvailableSlotsAsync(ClinicId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Could not reach the server.", HttpStatusCode.ServiceUnavailable));

        var cut = _ctx.Render<SlotPicker>(p => p.Add(x => x.ClinicId, ClinicId));

        Assert.Contains("Could not reach the server.", cut.Markup);
    }
}
