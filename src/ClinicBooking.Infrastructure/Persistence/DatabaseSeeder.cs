using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Scheduling;
using ClinicBooking.Contracts.Auth;
using ClinicBooking.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClinicBooking.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>When true, demo clinics, slots and users are created on startup. Development only.</summary>
    public bool Enabled { get; set; }

    /// <summary>Password given to the demo accounts. Set in appsettings.Development.json.</summary>
    public string DemoPassword { get; set; } = string.Empty;

    public int DaysOfSlots { get; set; } = 14;
}

/// <summary>Applies migrations and loads demo data so the system can be tried straight away.</summary>
public sealed class DatabaseSeeder(
    AppDbContext db,
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IClinicClock clock,
    IOptions<SeedOptions> options,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        foreach (var role in new[] { Roles.Patient, Roles.Staff, Roles.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        await SeedUsersAsync(settings.DemoPassword);
        await SeedClinicsAndSlotsAsync(settings.DaysOfSlots, ct);
    }

    private async Task SeedUsersAsync(string password)
    {
        await EnsureUserAsync("admin@phcis.local", password, Roles.Admin);
        await EnsureUserAsync("staff@phcis.local", password, Roles.Staff);

        var patientUser = await EnsureUserAsync("patient@phcis.local", password, Roles.Patient);
        if (!await db.Patients.AnyAsync(p => p.UserId == patientUser.Id))
        {
            db.Patients.Add(new Patient
            {
                UserId = patientUser.Id,
                FirstName = "Thandi",
                LastName = "Demo",
                IdNumber = "9001014800089",
                DateOfBirth = new DateOnly(1990, 1, 1),
                PhoneNumber = "0821234567",
                Email = patientUser.Email!,
            });
            await db.SaveChangesAsync();
        }
    }

    private async Task<IdentityUser> EnsureUserAsync(string email, string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Could not seed {email}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(user, role);
            logger.LogInformation("Seeded demo user {Email} with role {Role}", email, role);
        }

        return user;
    }

    private async Task SeedClinicsAndSlotsAsync(int days, CancellationToken ct)
    {
        if (!await db.Clinics.AnyAsync(ct))
        {
            db.Clinics.AddRange(
                new Clinic { Name = "Riverside Community Clinic", Address = "12 Main Road, Riverside", District = "Central District", PhoneNumber = "0115550101" },
                new Clinic { Name = "Hillview Primary Health Care Clinic", Address = "48 Hill Street, Hillview", District = "Northern District", PhoneNumber = "0115550202" },
                new Clinic { Name = "Northgate Community Health Centre", Address = "3 Gate Avenue, Northgate", District = "Northern District", PhoneNumber = "0115550303" });
            await db.SaveChangesAsync(ct);
        }

        // Top up slots so there is always availability for the next two weeks, however old the database is.
        var today = DateOnly.FromDateTime(clock.Now);
        var lastDay = today.AddDays(days - 1);

        foreach (var clinic in await db.Clinics.ToListAsync(ct))
        {
            var latestSlot = await db.TimeSlots.Where(s => s.ClinicId == clinic.Id).MaxAsync(s => (DateTime?)s.StartTime, ct);
            var firstDay = latestSlot is null ? today : DateOnly.FromDateTime(latestSlot.Value).AddDays(1);
            if (firstDay < today)
            {
                firstDay = today;
            }

            if (firstDay > lastDay)
            {
                continue;
            }

            var slots = SlotGenerator.Generate(
                clinic.Id, firstDay, lastDay, new TimeOnly(8, 0), new TimeOnly(16, 0),
                TimeSpan.FromMinutes(30), capacity: 2, includeWeekends: false);

            db.TimeSlots.AddRange(slots);
            logger.LogInformation("Seeded {Count} slots for {Clinic}", slots.Count, clinic.Name);
        }

        await db.SaveChangesAsync(ct);
    }
}
