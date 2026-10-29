using FluentValidation;

namespace ClinicBooking.Contracts.Clinics;

public sealed record ClinicDto(Guid Id, string Name, string Address, string District, string PhoneNumber);

public sealed class CreateClinicRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public sealed class CreateClinicRequestValidator : AbstractValidator<CreateClinicRequest>
{
    public CreateClinicRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);
        RuleFor(x => x.District).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(ValidationPatterns.PhoneNumber)
            .WithMessage(ValidationPatterns.PhoneNumberMessage);
    }
}
