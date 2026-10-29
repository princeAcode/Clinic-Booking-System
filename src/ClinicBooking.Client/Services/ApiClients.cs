using ClinicBooking.Contracts.Appointments;
using ClinicBooking.Contracts.Auth;
using ClinicBooking.Contracts.Clinics;
using ClinicBooking.Contracts.Slots;

namespace ClinicBooking.Client.Services;

public interface IClinicApi
{
    Task<IReadOnlyList<ClinicDto>> GetClinicsAsync(CancellationToken ct = default);
    Task<ClinicDto> GetClinicAsync(Guid clinicId, CancellationToken ct = default);
    Task<IReadOnlyList<TimeSlotDto>> GetAvailableSlotsAsync(Guid clinicId, DateOnly date, CancellationToken ct = default);
    Task<GenerateSlotsResponse> GenerateSlotsAsync(Guid clinicId, GenerateSlotsRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentDto>> GetClinicAppointmentsAsync(Guid clinicId, DateOnly date, CancellationToken ct = default);
}

public interface IAppointmentApi
{
    Task<BookingConfirmationDto> BookAsync(BookAppointmentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentDto>> GetMineAsync(CancellationToken ct = default);
    Task<AppointmentDto> GetAsync(Guid appointmentId, CancellationToken ct = default);
    Task<AppointmentDto> CancelAsync(Guid appointmentId, CancellationToken ct = default);
    Task<BookingConfirmationDto> RescheduleAsync(Guid appointmentId, RescheduleAppointmentRequest request, CancellationToken ct = default);
}

public interface IAuthApi
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RegisterAsync(RegisterPatientRequest request, CancellationToken ct = default);
}

public sealed class ClinicApi(HttpClient http) : ApiClientBase(http), IClinicApi
{
    public Task<IReadOnlyList<ClinicDto>> GetClinicsAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<ClinicDto>>("api/clinics", ct);

    public Task<ClinicDto> GetClinicAsync(Guid clinicId, CancellationToken ct = default) =>
        GetAsync<ClinicDto>($"api/clinics/{clinicId}", ct);

    public Task<IReadOnlyList<TimeSlotDto>> GetAvailableSlotsAsync(Guid clinicId, DateOnly date, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<TimeSlotDto>>($"api/clinics/{clinicId}/slots?date={date:yyyy-MM-dd}", ct);

    public Task<GenerateSlotsResponse> GenerateSlotsAsync(Guid clinicId, GenerateSlotsRequest request, CancellationToken ct = default) =>
        PostAsync<GenerateSlotsResponse>($"api/clinics/{clinicId}/slots/generate", request, ct);

    public Task<IReadOnlyList<AppointmentDto>> GetClinicAppointmentsAsync(Guid clinicId, DateOnly date, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<AppointmentDto>>($"api/clinics/{clinicId}/appointments?date={date:yyyy-MM-dd}", ct);
}

public sealed class AppointmentApi(HttpClient http) : ApiClientBase(http), IAppointmentApi
{
    public Task<BookingConfirmationDto> BookAsync(BookAppointmentRequest request, CancellationToken ct = default) =>
        PostAsync<BookingConfirmationDto>("api/appointments", request, ct);

    public Task<IReadOnlyList<AppointmentDto>> GetMineAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<AppointmentDto>>("api/appointments/mine", ct);

    public Task<AppointmentDto> GetAsync(Guid appointmentId, CancellationToken ct = default) =>
        GetAsync<AppointmentDto>($"api/appointments/{appointmentId}", ct);

    public Task<AppointmentDto> CancelAsync(Guid appointmentId, CancellationToken ct = default) =>
        PostAsync<AppointmentDto>($"api/appointments/{appointmentId}/cancel", null, ct);

    public Task<BookingConfirmationDto> RescheduleAsync(Guid appointmentId, RescheduleAppointmentRequest request, CancellationToken ct = default) =>
        PostAsync<BookingConfirmationDto>($"api/appointments/{appointmentId}/reschedule", request, ct);
}

public sealed class AuthApi(HttpClient http) : ApiClientBase(http), IAuthApi
{
    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
        PostAsync<AuthResponse>("api/auth/login", request, ct);

    public Task<AuthResponse> RegisterAsync(RegisterPatientRequest request, CancellationToken ct = default) =>
        PostAsync<AuthResponse>("api/auth/register", request, ct);
}
