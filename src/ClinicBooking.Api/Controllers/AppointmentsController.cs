using ClinicBooking.Application.Services;
using ClinicBooking.Contracts.Appointments;
using ClinicBooking.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBooking.Api.Controllers;

/// <summary>Booking, viewing, cancelling and rescheduling appointments. Patients only see their own appointments.</summary>
[ApiController]
[Route("api/appointments")]
[Authorize]
public sealed class AppointmentsController(IAppointmentService appointmentService) : ControllerBase
{
    /// <summary>Books a time slot for the signed-in patient and returns a confirmation.</summary>
    /// <response code="201">Booked. The body contains the confirmation message and reference number.</response>
    /// <response code="409">The slot is full, or the patient already has an appointment at that time.</response>
    /// <response code="422">The slot is in the past.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Patient)]
    [ProducesResponseType<BookingConfirmationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingConfirmationDto>> Book(BookAppointmentRequest request, CancellationToken ct)
    {
        var confirmation = await appointmentService.BookAsync(request, ct);
        return CreatedAtAction(nameof(GetAppointment), new { appointmentId = confirmation.Appointment.Id }, confirmation);
    }

    /// <summary>The signed-in patient's appointments, newest first.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<IReadOnlyList<AppointmentDto>> GetMine(CancellationToken ct) =>
        await appointmentService.GetMyAppointmentsAsync(ct);

    /// <summary>Gets one appointment.</summary>
    [HttpGet("{appointmentId:guid}")]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<AppointmentDto> GetAppointment(Guid appointmentId, CancellationToken ct) =>
        await appointmentService.GetAsync(appointmentId, ct);

    /// <summary>Cancels an appointment and frees its slot.</summary>
    [HttpPost("{appointmentId:guid}/cancel")]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<AppointmentDto> Cancel(Guid appointmentId, CancellationToken ct) =>
        await appointmentService.CancelAsync(appointmentId, ct);

    /// <summary>Moves an appointment to another slot, keeping its reference number.</summary>
    [HttpPost("{appointmentId:guid}/reschedule")]
    [ProducesResponseType<BookingConfirmationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<BookingConfirmationDto> Reschedule(Guid appointmentId, RescheduleAppointmentRequest request, CancellationToken ct) =>
        await appointmentService.RescheduleAsync(appointmentId, request, ct);
}
