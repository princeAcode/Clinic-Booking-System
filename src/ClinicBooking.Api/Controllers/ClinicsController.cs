using ClinicBooking.Application.Services;
using ClinicBooking.Contracts.Appointments;
using ClinicBooking.Contracts.Auth;
using ClinicBooking.Contracts.Clinics;
using ClinicBooking.Contracts.Slots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBooking.Api.Controllers;

[ApiController]
[Route("api/clinics")]
public sealed class ClinicsController(
    IClinicService clinicService,
    ITimeSlotService slotService,
    IAppointmentService appointmentService) : ControllerBase
{
    /// <summary>Lists all clinics.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<ClinicDto>> GetClinics(CancellationToken ct) =>
        await clinicService.GetClinicsAsync(ct);

    /// <summary>Gets one clinic.</summary>
    [HttpGet("{clinicId:guid}")]
    [ProducesResponseType<ClinicDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ClinicDto> GetClinic(Guid clinicId, CancellationToken ct) =>
        await clinicService.GetClinicAsync(clinicId, ct);

    /// <summary>Adds a clinic. Administrators only.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ClinicDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClinicDto>> CreateClinic(CreateClinicRequest request, CancellationToken ct)
    {
        var clinic = await clinicService.CreateClinicAsync(request, ct);
        return CreatedAtAction(nameof(GetClinic), new { clinicId = clinic.Id }, clinic);
    }

    /// <summary>Lists the slots that can still be booked at a clinic on a given day.</summary>
    /// <param name="clinicId">The clinic.</param>
    /// <param name="date">The day, in yyyy-MM-dd format (clinic local date).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{clinicId:guid}/slots")]
    [ProducesResponseType<IReadOnlyList<TimeSlotDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<TimeSlotDto>> GetAvailableSlots(Guid clinicId, [FromQuery] DateOnly date, CancellationToken ct) =>
        await slotService.GetAvailableSlotsAsync(clinicId, date, ct);

    /// <summary>Opens appointment slots for a range of days. Clinic staff only.</summary>
    [HttpPost("{clinicId:guid}/slots/generate")]
    [Authorize(Roles = Roles.StaffOrAdmin)]
    [ProducesResponseType<GenerateSlotsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<GenerateSlotsResponse> GenerateSlots(Guid clinicId, GenerateSlotsRequest request, CancellationToken ct) =>
        await slotService.GenerateSlotsAsync(clinicId, request, ct);

    /// <summary>The clinic's appointment list for a day (all statuses). Clinic staff only.</summary>
    [HttpGet("{clinicId:guid}/appointments")]
    [Authorize(Roles = Roles.StaffOrAdmin)]
    public async Task<IReadOnlyList<AppointmentDto>> GetClinicAppointments(Guid clinicId, [FromQuery] DateOnly date, CancellationToken ct) =>
        await appointmentService.GetClinicAppointmentsAsync(clinicId, date, ct);
}
