using ClinicBooking.Contracts;
using ClinicBooking.Contracts.Auth;
using ClinicBooking.Contracts.Slots;

namespace ClinicBooking.UnitTests.Validation;

public sealed class ValidatorTests
{
    [Theory]
    [InlineData("9001014800089", true)]   // valid SA ID (Luhn check digit 9)
    [InlineData("9001014800086", false)]  // wrong check digit
    [InlineData("A12345678", true)]       // passport
    [InlineData("12-34", false)]
    [InlineData("", false)]
    public void IdOrPassportNumber_IsValidated(string value, bool expected)
    {
        Assert.Equal(expected, ValidationPatterns.IsValidIdOrPassportNumber(value));
    }

    [Fact]
    public void RegisterPatient_ValidRequest_Passes()
    {
        var result = new RegisterPatientRequestValidator().Validate(ValidRegistration());

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void RegisterPatient_MismatchedPasswordsAndBadPhone_Fails()
    {
        var request = ValidRegistration();
        request.ConfirmPassword = "Different1";
        request.PhoneNumber = "12345";

        var result = new RegisterPatientRequestValidator().Validate(request);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterPatientRequest.ConfirmPassword));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterPatientRequest.PhoneNumber));
    }

    [Fact]
    public void GenerateSlots_ClosingBeforeOpening_Fails()
    {
        var request = new GenerateSlotsRequest
        {
            FromDate = new DateTime(2026, 11, 2),
            ToDate = new DateTime(2026, 11, 6),
            DayStart = new TimeSpan(16, 0, 0),
            DayEnd = new TimeSpan(8, 0, 0),
        };

        var result = new GenerateSlotsRequestValidator().Validate(request);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GenerateSlotsRequest.DayEnd));
    }

    [Fact]
    public void GenerateSlots_RangeLongerThanAMonth_Fails()
    {
        var request = new GenerateSlotsRequest
        {
            FromDate = new DateTime(2026, 11, 1),
            ToDate = new DateTime(2026, 12, 31),
        };

        var result = new GenerateSlotsRequestValidator().Validate(request);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GenerateSlotsRequest.ToDate));
    }

    private static RegisterPatientRequest ValidRegistration() => new()
    {
        FirstName = "Thandi",
        LastName = "Mokoena",
        IdNumber = "9001014800089",
        DateOfBirth = new DateTime(1990, 1, 1),
        PhoneNumber = "082 123 4567",
        Email = "thandi@example.com",
        Password = "Secret123",
        ConfirmPassword = "Secret123",
    };
}
