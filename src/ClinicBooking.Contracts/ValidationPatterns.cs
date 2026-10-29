namespace ClinicBooking.Contracts;

/// <summary>Shared validation helpers so the client and API apply exactly the same rules.</summary>
public static class ValidationPatterns
{
    /// <summary>South African numbers: 0XXXXXXXXX or +27XXXXXXXXX, spaces allowed.</summary>
    public const string PhoneNumber = @"^(\+27|0)[1-9](\s?\d){8}$";

    public const string PhoneNumberMessage = "Enter a valid South African phone number, e.g. 082 123 4567 or +27 82 123 4567.";

    /// <summary>
    /// Accepts a 13-digit South African ID number (checked with the Luhn algorithm)
    /// or a 6–20 character alphanumeric passport number.
    /// </summary>
    public static bool IsValidIdOrPassportNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Length == 13 && value.All(char.IsAsciiDigit))
        {
            return PassesLuhnCheck(value);
        }

        return value.Length is >= 6 and <= 20 && value.All(char.IsAsciiLetterOrDigit);
    }

    /// <summary>The Luhn checksum used for the last digit of South African ID numbers.</summary>
    public static bool PassesLuhnCheck(string digits)
    {
        var sum = 0;
        var doubleDigit = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }
}
