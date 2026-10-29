using ClinicBooking.Contracts.Auth;

namespace ClinicBooking.Application.Abstractions;

/// <summary>The signed-in user making the current request.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Set when the user is a patient.</summary>
    Guid? PatientId { get; }

    /// <summary>Clinic staff and administrators can see and manage all appointments.</summary>
    bool IsStaff { get; }
}

/// <summary>
/// The current time in the clinics' time zone. Slot times are stored as clinic-local times,
/// so "is this in the past?" checks must compare against this clock, not UTC.
/// </summary>
public interface IClinicClock
{
    DateTime Now { get; }
    DateTime UtcNow { get; }
}

public interface IReferenceNumberGenerator
{
    string Generate();
}

/// <summary>Account registration and sign-in. Implemented with ASP.NET Identity in the infrastructure layer.</summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterPatientAsync(RegisterPatientRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}
