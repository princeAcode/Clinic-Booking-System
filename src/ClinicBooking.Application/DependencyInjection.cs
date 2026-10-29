using ClinicBooking.Application.Services;
using ClinicBooking.Contracts.Auth;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicBooking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Validators live in the Contracts project so the Blazor client can run the same rules.
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(ServiceLifetime.Singleton);

        services.AddScoped<IClinicService, ClinicService>();
        services.AddScoped<ITimeSlotService, TimeSlotService>();
        services.AddScoped<IAppointmentService, AppointmentService>();

        return services;
    }
}
