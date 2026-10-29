using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Exceptions;
using ClinicBooking.Contracts.Auth;
using ClinicBooking.Domain.Entities;
using ClinicBooking.Domain.Exceptions;
using ClinicBooking.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClinicBooking.Infrastructure.Identity;

internal sealed class AuthService(
    UserManager<IdentityUser> userManager,
    AppDbContext db,
    IPatientRepository patients,
    IJwtTokenGenerator tokens,
    IValidator<RegisterPatientRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> RegisterPatientAsync(RegisterPatientRequest request, CancellationToken ct = default)
    {
        await registerValidator.ValidateAndThrowAsync(request, ct);

        var email = request.Email.Trim();
        var idNumber = request.IdNumber.Trim().ToUpperInvariant();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new BusinessRuleException("An account with this email address already exists.");
        }

        if (await patients.IdNumberExistsAsync(idNumber, ct))
        {
            throw new BusinessRuleException("A patient with this ID number is already registered.");
        }

        // The Identity user and the patient record are created together or not at all.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var user = new IdentityUser { UserName = email, Email = email };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new BusinessRuleException(string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, Roles.Patient);

        var patient = new Patient
        {
            UserId = user.Id,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            IdNumber = idNumber,
            DateOfBirth = DateOnly.FromDateTime(request.DateOfBirth!.Value),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = email,
        };
        patients.Add(patient);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("Registered patient {PatientId}", patient.Id);

        return tokens.CreateToken(user.Id, email, patient.FullName, [Roles.Patient], patient.Id);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            // Same message for unknown email and wrong password, so accounts cannot be enumerated.
            throw new AuthenticationFailedException("Incorrect email or password.");
        }

        var roles = await userManager.GetRolesAsync(user);
        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        var displayName = patient?.FullName ?? user.Email!;

        return tokens.CreateToken(user.Id, user.Email!, displayName, roles, patient?.Id);
    }
}
