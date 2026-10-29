using System.Security.Claims;
using System.Text;
using ClinicBooking.Application.Abstractions;
using ClinicBooking.Contracts.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClinicBooking.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ClinicBooking";
    public string Audience { get; set; } = "ClinicBooking";

    /// <summary>HMAC signing key, at least 32 characters. Supply through configuration or a secret store, never source control in production.</summary>
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 120;

    public SymmetricSecurityKey GetSigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

/// <summary>Short claim names used in tokens; the API and the Blazor client both read these.</summary>
public static class AppClaims
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";
    public const string PatientId = "patient_id";
}

public interface IJwtTokenGenerator
{
    AuthResponse CreateToken(string userId, string email, string displayName, IEnumerable<string> roles, Guid? patientId);
}

internal sealed class JwtTokenGenerator(IOptions<JwtOptions> options, IClinicClock clock) : IJwtTokenGenerator
{
    public AuthResponse CreateToken(string userId, string email, string displayName, IEnumerable<string> roles, Guid? patientId)
    {
        var settings = options.Value;
        var expires = clock.UtcNow.AddMinutes(settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(AppClaims.Subject, userId),
            new(AppClaims.Email, email),
            new(AppClaims.Name, displayName),
        };
        claims.AddRange(roles.Select(role => new Claim(AppClaims.Role, role)));
        if (patientId is not null)
        {
            claims.Add(new Claim(AppClaims.PatientId, patientId.Value.ToString()));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            SigningCredentials = new SigningCredentials(settings.GetSigningKey(), SecurityAlgorithms.HmacSha256),
        };

        return new AuthResponse(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
