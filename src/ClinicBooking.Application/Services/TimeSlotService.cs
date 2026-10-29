using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Exceptions;
using ClinicBooking.Application.Mapping;
using ClinicBooking.Application.Scheduling;
using ClinicBooking.Contracts.Slots;
using ClinicBooking.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ClinicBooking.Application.Services;

public interface ITimeSlotService
{
    /// <summary>Slots on the given day that can still be booked (in the future and not full).</summary>
    Task<IReadOnlyList<TimeSlotDto>> GetAvailableSlotsAsync(Guid clinicId, DateOnly date, CancellationToken ct = default);

    /// <summary>Opens new slots for a clinic. Fails if any of them would overlap existing slots.</summary>
    Task<GenerateSlotsResponse> GenerateSlotsAsync(Guid clinicId, GenerateSlotsRequest request, CancellationToken ct = default);
}

public sealed class TimeSlotService(
    IClinicRepository clinics,
    ITimeSlotRepository slots,
    IUnitOfWork unitOfWork,
    IClinicClock clock,
    IValidator<GenerateSlotsRequest> generateValidator,
    ILogger<TimeSlotService> logger) : ITimeSlotService
{
    public async Task<IReadOnlyList<TimeSlotDto>> GetAvailableSlotsAsync(Guid clinicId, DateOnly date, CancellationToken ct = default)
    {
        if (!await clinics.ExistsAsync(clinicId, ct))
        {
            throw new NotFoundException("Clinic", clinicId);
        }

        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var now = clock.Now;

        // Never offer slots that have already started.
        var from = dayStart > now ? dayStart : now;
        var to = dayStart.AddDays(1);

        if (from >= to)
        {
            return [];
        }

        var available = await slots.GetAvailableAsync(clinicId, from, to, ct);
        return available.Select(s => s.ToDto()).ToList();
    }

    public async Task<GenerateSlotsResponse> GenerateSlotsAsync(Guid clinicId, GenerateSlotsRequest request, CancellationToken ct = default)
    {
        await generateValidator.ValidateAndThrowAsync(request, ct);

        if (!await clinics.ExistsAsync(clinicId, ct))
        {
            throw new NotFoundException("Clinic", clinicId);
        }

        var fromDate = DateOnly.FromDateTime(request.FromDate!.Value);
        var toDate = DateOnly.FromDateTime(request.ToDate!.Value);

        if (fromDate < DateOnly.FromDateTime(clock.Now))
        {
            throw new BusinessRuleException("Slots cannot be created for dates in the past.");
        }

        var newSlots = SlotGenerator.Generate(
            clinicId,
            fromDate,
            toDate,
            TimeOnly.FromTimeSpan(request.DayStart!.Value),
            TimeOnly.FromTimeSpan(request.DayEnd!.Value),
            TimeSpan.FromMinutes(request.SlotDurationMinutes),
            request.CapacityPerSlot,
            request.IncludeWeekends);

        if (newSlots.Count == 0)
        {
            throw new BusinessRuleException("No slots fit the chosen dates and hours.");
        }

        if (await slots.AnyOverlappingAsync(clinicId, newSlots[0].StartTime, newSlots[^1].EndTime, ct))
        {
            throw new BookingConflictException("Slots already exist for part of this period. Choose dates that have no slots yet.");
        }

        slots.AddRange(newSlots);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Generated {SlotCount} slots for clinic {ClinicId} from {FromDate} to {ToDate}",
            newSlots.Count, clinicId, fromDate, toDate);

        return new GenerateSlotsResponse(newSlots.Count);
    }
}
