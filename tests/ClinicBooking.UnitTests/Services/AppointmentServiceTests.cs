using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Exceptions;
using ClinicBooking.Application.Services;
using ClinicBooking.Contracts.Appointments;
using ClinicBooking.Domain.Entities;
using ClinicBooking.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ClinicBooking.UnitTests.Services;

public sealed class AppointmentServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 9, 0, 0);

    private readonly Mock<IAppointmentRepository> _appointments = new();
    private readonly Mock<ITimeSlotRepository> _slots = new();
    private readonly Mock<IPatientRepository> _patients = new();
    private readonly Mock<IClinicRepository> _clinics = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClinicClock> _clock = new();
    private readonly Mock<IReferenceNumberGenerator> _referenceNumbers = new();

    private readonly Clinic _clinic = TestData.Clinic();
    private readonly Patient _patient = TestData.Patient();

    public AppointmentServiceTests()
    {
        _clock.Setup(c => c.Now).Returns(Now);
        _clock.Setup(c => c.UtcNow).Returns(Now.AddHours(-2));
        _referenceNumbers.Setup(r => r.Generate()).Returns("PHC-TEST0001");
        _patients.Setup(p => p.GetByIdAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_patient);
        SignInAsPatient(_patient.Id);
    }

    private AppointmentService CreateService() => new(
        _appointments.Object,
        _slots.Object,
        _patients.Object,
        _clinics.Object,
        _unitOfWork.Object,
        _currentUser.Object,
        _clock.Object,
        _referenceNumbers.Object,
        new BookAppointmentRequestValidator(),
        new RescheduleAppointmentRequestValidator(),
        NullLogger<AppointmentService>.Instance);

    private void SignInAsPatient(Guid patientId)
    {
        _currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUser.Setup(u => u.PatientId).Returns(patientId);
        _currentUser.Setup(u => u.IsStaff).Returns(false);
    }

    private void SignInAsStaff()
    {
        _currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUser.Setup(u => u.PatientId).Returns((Guid?)null);
        _currentUser.Setup(u => u.IsStaff).Returns(true);
    }

    private TimeSlot GivenSlot(DateTime start, int capacity = 1, int booked = 0)
    {
        var slot = TestData.Slot(_clinic, start, capacity, booked);
        _slots.Setup(s => s.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        return slot;
    }

    private Appointment GivenBookedAppointment(TimeSlot slot, Patient? patient = null)
    {
        var appointment = Appointment.Book(patient ?? _patient, slot, "PHC-EXISTING", null, Now.AddDays(-1));
        _appointments.Setup(a => a.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);
        return appointment;
    }

    // ---- Booking ----

    [Fact]
    public async Task Book_AvailableFutureSlot_CreatesAppointmentReservesPlaceAndReturnsConfirmation()
    {
        var slot = GivenSlot(Now.AddDays(1), capacity: 2);

        var result = await CreateService().BookAsync(new BookAppointmentRequest { TimeSlotId = slot.Id, Reason = "  Check-up  " });

        Assert.Equal(1, slot.BookedCount);
        Assert.Equal("PHC-TEST0001", result.Appointment.ReferenceNumber);
        Assert.Equal("Booked", result.Appointment.Status);
        Assert.Equal("Check-up", result.Appointment.Reason);
        Assert.Contains("confirmed", result.Message);
        Assert.Contains("PHC-TEST0001", result.Message);
        Assert.Contains(_clinic.Name, result.Message);
        _appointments.Verify(a => a.Add(It.Is<Appointment>(x => x.TimeSlotId == slot.Id && x.PatientId == _patient.Id)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Book_FullSlot_ThrowsConflictAndSavesNothing()
    {
        var slot = GivenSlot(Now.AddDays(1), capacity: 1, booked: 1);

        await Assert.ThrowsAsync<BookingConflictException>(() =>
            CreateService().BookAsync(new BookAppointmentRequest { TimeSlotId = slot.Id }));

        _appointments.Verify(a => a.Add(It.IsAny<Appointment>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Book_PatientAlreadyBookedAtOverlappingTime_ThrowsConflict()
    {
        var slot = GivenSlot(Now.AddDays(1), capacity: 5);
        _appointments
            .Setup(a => a.PatientHasOverlappingAsync(_patient.Id, slot.StartTime, slot.EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<BookingConflictException>(() =>
            CreateService().BookAsync(new BookAppointmentRequest { TimeSlotId = slot.Id }));

        Assert.Contains("already have an appointment", ex.Message);
        Assert.Equal(0, slot.BookedCount);
    }

    [Fact]
    public async Task Book_SlotInThePast_ThrowsBusinessRule()
    {
        var slot = GivenSlot(Now.AddMinutes(-30));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService().BookAsync(new BookAppointmentRequest { TimeSlotId = slot.Id }));
    }

    [Fact]
    public async Task Book_SlotStartingNow_ThrowsBusinessRule()
    {
        var slot = GivenSlot(Now);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService().BookAsync(new BookAppointmentRequest { TimeSlotId = slot.Id }));
    }

    [Fact]
    public async Task Book_UnknownSlot_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService().BookAsync(new BookAppointmentRequest { TimeSlotId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task Book_EmptySlotId_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().BookAsync(new BookAppointmentRequest { TimeSlotId = Guid.Empty }));
    }

    [Fact]
    public async Task Book_ByStaffUserWithoutPatientRecord_ThrowsForbidden()
    {
        SignInAsStaff();
        var slot = GivenSlot(Now.AddDays(1));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            CreateService().BookAsync(new BookAppointmentRequest { TimeSlotId = slot.Id }));
    }

    // ---- Cancellation ----

    [Fact]
    public async Task Cancel_OwnFutureAppointment_CancelsAndReleasesPlace()
    {
        var slot = GivenSlot(Now.AddDays(1));
        var appointment = GivenBookedAppointment(slot);
        Assert.Equal(1, slot.BookedCount);

        var result = await CreateService().CancelAsync(appointment.Id);

        Assert.Equal("Cancelled", result.Status);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Equal(0, slot.BookedCount);
        Assert.NotNull(appointment.CancelledAtUtc);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_AnotherPatientsAppointment_ThrowsNotFoundSoIdsCannotBeProbed()
    {
        var slot = GivenSlot(Now.AddDays(1));
        var appointment = GivenBookedAppointment(slot, TestData.Patient());

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().CancelAsync(appointment.Id));
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
    }

    [Fact]
    public async Task Cancel_ByStaff_CanCancelAnyPatientsAppointment()
    {
        var slot = GivenSlot(Now.AddDays(1));
        var appointment = GivenBookedAppointment(slot, TestData.Patient());
        SignInAsStaff();

        await CreateService().CancelAsync(appointment.Id);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ThrowsBusinessRuleAndDoesNotReleaseTwice()
    {
        var slot = GivenSlot(Now.AddDays(1), capacity: 3);
        var appointment = GivenBookedAppointment(slot);
        Appointment.Book(TestData.Patient(), slot, "PHC-OTHER", null, Now); // another patient holds a place
        appointment.Cancel(Now);
        Assert.Equal(1, slot.BookedCount);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CancelAsync(appointment.Id));
        Assert.Equal(1, slot.BookedCount);
    }

    [Fact]
    public async Task Cancel_AppointmentThatHasStarted_ThrowsBusinessRule()
    {
        var slot = GivenSlot(Now.AddDays(1));
        var appointment = GivenBookedAppointment(slot);
        slot.StartTime = Now.AddMinutes(-5);
        slot.EndTime = Now.AddMinutes(25);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CancelAsync(appointment.Id));
    }

    // ---- Rescheduling ----

    [Fact]
    public async Task Reschedule_ToAvailableSlot_MovesAppointmentAndSwapsCapacity()
    {
        var oldSlot = GivenSlot(Now.AddDays(1));
        var newSlot = GivenSlot(Now.AddDays(2));
        var appointment = GivenBookedAppointment(oldSlot);

        var result = await CreateService().RescheduleAsync(appointment.Id, new RescheduleAppointmentRequest { NewTimeSlotId = newSlot.Id });

        Assert.Equal(newSlot.Id, appointment.TimeSlotId);
        Assert.Equal(0, oldSlot.BookedCount);
        Assert.Equal(1, newSlot.BookedCount);
        Assert.Equal("PHC-EXISTING", result.Appointment.ReferenceNumber);
        Assert.Contains("rescheduled", result.Message);
        _appointments.Verify(a => a.PatientHasOverlappingAsync(_patient.Id, newSlot.StartTime, newSlot.EndTime, appointment.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reschedule_ToFullSlot_ThrowsConflictAndKeepsOriginalBooking()
    {
        var oldSlot = GivenSlot(Now.AddDays(1));
        var fullSlot = GivenSlot(Now.AddDays(2), capacity: 1, booked: 1);
        var appointment = GivenBookedAppointment(oldSlot);

        await Assert.ThrowsAsync<BookingConflictException>(() =>
            CreateService().RescheduleAsync(appointment.Id, new RescheduleAppointmentRequest { NewTimeSlotId = fullSlot.Id }));

        Assert.Equal(oldSlot.Id, appointment.TimeSlotId);
        Assert.Equal(1, oldSlot.BookedCount);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reschedule_ToSameSlot_ThrowsBusinessRule()
    {
        var slot = GivenSlot(Now.AddDays(1), capacity: 2);
        var appointment = GivenBookedAppointment(slot);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService().RescheduleAsync(appointment.Id, new RescheduleAppointmentRequest { NewTimeSlotId = slot.Id }));

        Assert.Equal(1, slot.BookedCount);
    }

    [Fact]
    public async Task Reschedule_ToTimeClashingWithAnotherOfThePatientsAppointments_ThrowsConflict()
    {
        var oldSlot = GivenSlot(Now.AddDays(1));
        var newSlot = GivenSlot(Now.AddDays(2));
        var appointment = GivenBookedAppointment(oldSlot);
        _appointments
            .Setup(a => a.PatientHasOverlappingAsync(_patient.Id, newSlot.StartTime, newSlot.EndTime, appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<BookingConflictException>(() =>
            CreateService().RescheduleAsync(appointment.Id, new RescheduleAppointmentRequest { NewTimeSlotId = newSlot.Id }));

        Assert.Equal(oldSlot.Id, appointment.TimeSlotId);
    }

    // ---- Queries ----

    [Fact]
    public async Task GetClinicAppointments_AsPatient_ThrowsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            CreateService().GetClinicAppointmentsAsync(_clinic.Id, DateOnly.FromDateTime(Now)));
    }

    [Fact]
    public async Task GetMyAppointments_MarksOnlyFutureActiveAppointmentsAsModifiable()
    {
        var future = Appointment.Book(_patient, TestData.Slot(_clinic, Now.AddDays(1)), "PHC-FUTURE", null, Now);
        var past = Appointment.Book(_patient, TestData.Slot(_clinic, Now.AddDays(-1)), "PHC-PAST", null, Now);
        var cancelled = Appointment.Book(_patient, TestData.Slot(_clinic, Now.AddDays(2)), "PHC-CANCEL", null, Now);
        cancelled.Cancel(Now);
        _appointments.Setup(a => a.GetForPatientAsync(_patient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([future, past, cancelled]);

        var result = await CreateService().GetMyAppointmentsAsync();

        Assert.True(result.Single(a => a.ReferenceNumber == "PHC-FUTURE").CanModify);
        Assert.False(result.Single(a => a.ReferenceNumber == "PHC-PAST").CanModify);
        Assert.False(result.Single(a => a.ReferenceNumber == "PHC-CANCEL").CanModify);
    }
}
