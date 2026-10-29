using FluentValidation;

namespace ClinicBooking.Contracts.Auth;

public static class Roles
{
    public const string Patient = "Patient";
    public const string Staff = "Staff";
    public const string Admin = "Admin";

    /// <summary>Comma-separated list for <c>[Authorize(Roles = ...)]</c> on staff features.</summary>
    public const string StaffOrAdmin = Staff + "," + Admin;
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterPatientRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed record AuthResponse(string Token, DateTime ExpiresAtUtc);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class RegisterPatientRequestValidator : AbstractValidator<RegisterPatientRequest>
{
    public RegisterPatientRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

        RuleFor(x => x.IdNumber)
            .NotEmpty()
            .Must(ValidationPatterns.IsValidIdOrPassportNumber)
            .WithMessage("Enter a valid 13-digit SA ID number or a passport number.");

        RuleFor(x => x.DateOfBirth)
            .NotNull().WithMessage("Enter your date of birth.")
            .LessThan(_ => DateTime.Today).WithMessage("Date of birth must be in the past.")
            .GreaterThan(_ => DateTime.Today.AddYears(-130)).WithMessage("Enter a realistic date of birth.");

        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(ValidationPatterns.PhoneNumber)
            .WithMessage(ValidationPatterns.PhoneNumberMessage);

        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}
