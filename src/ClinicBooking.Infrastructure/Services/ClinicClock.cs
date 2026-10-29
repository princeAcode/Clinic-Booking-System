using System.Security.Cryptography;
using ClinicBooking.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace ClinicBooking.Infrastructure.Services;

public sealed class ClinicOptions
{
    public const string SectionName = "Clinic";

    /// <summary>IANA time zone the clinics operate in. Slot times are stored in this zone.</summary>
    public string TimeZone { get; set; } = "Africa/Johannesburg";
}

internal sealed class ClinicClock(TimeProvider timeProvider, IOptions<ClinicOptions> options) : IClinicClock
{
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _timeZone);
}

/// <summary>Generates codes like PHC-7K2M9QXA. Ambiguous characters (0/O, 1/I) are left out so codes are easy to read over the phone.</summary>
internal sealed class ReferenceNumberGenerator : IReferenceNumberGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string Generate() => "PHC-" + RandomNumberGenerator.GetString(Alphabet, 8);
}
