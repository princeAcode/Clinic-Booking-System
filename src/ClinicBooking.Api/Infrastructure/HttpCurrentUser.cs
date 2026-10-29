using ClinicBooking.Application.Abstractions;
using ClinicBooking.Contracts.Auth;
using ClinicBooking.Infrastructure.Identity;

namespace ClinicBooking.Api.Infrastructure;

/// <summary>Reads the signed-in user from the JWT claims on the current HTTP request.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? PatientId =>
        Guid.TryParse(User?.FindFirst(AppClaims.PatientId)?.Value, out var id) ? id : null;

    public bool IsStaff => User is not null && (User.IsInRole(Roles.Staff) || User.IsInRole(Roles.Admin));
}
