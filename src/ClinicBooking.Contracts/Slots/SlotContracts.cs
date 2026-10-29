using FluentValidation;

namespace ClinicBooking.Contracts.Slots;

/// <summary>A bookable slot. Times are in the clinic's local time.</summary>
public sealed record TimeSlotDto(
    Guid Id,
    Guid ClinicId,
    DateTime StartTime,
    DateTime EndTime,
    int Capacity,
    int RemainingCapacity);

/// <summary>Staff request to open appointment slots for a clinic over a range of days.</summary>
public sealed class GenerateSlotsRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public TimeSpan? DayStart { get; set; } = new(8, 0, 0);
    public TimeSpan? DayEnd { get; set; } = new(16, 0, 0);
    public int SlotDurationMinutes { get; set; } = 30;
    public int CapacityPerSlot { get; set; } = 1;
    public bool IncludeWeekends { get; set; }
}

public sealed record GenerateSlotsResponse(int SlotsCreated);

public sealed class GenerateSlotsRequestValidator : AbstractValidator<GenerateSlotsRequest>
{
    public const int MaxDaysPerRequest = 31;

    public GenerateSlotsRequestValidator()
    {
        RuleFor(x => x.FromDate).NotNull().WithMessage("Choose a start date.");
        RuleFor(x => x.ToDate).NotNull().WithMessage("Choose an end date.")
            .GreaterThanOrEqualTo(x => x.FromDate).When(x => x.FromDate.HasValue)
            .WithMessage("End date cannot be before the start date.")
            .Must((request, to) => (to!.Value.Date - request.FromDate!.Value.Date).TotalDays < MaxDaysPerRequest)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage($"You can generate at most {MaxDaysPerRequest} days at a time.");

        RuleFor(x => x.DayStart).NotNull().WithMessage("Choose the opening time.");
        RuleFor(x => x.DayEnd).NotNull().WithMessage("Choose the closing time.")
            .GreaterThan(x => x.DayStart).When(x => x.DayStart.HasValue)
            .WithMessage("Closing time must be after opening time.");

        RuleFor(x => x.SlotDurationMinutes).InclusiveBetween(5, 240);
        RuleFor(x => x.CapacityPerSlot).InclusiveBetween(1, 50);
    }
}
