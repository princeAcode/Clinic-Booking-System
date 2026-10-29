namespace ClinicBooking.Domain.Exceptions;

/// <summary>Base type for errors caused by breaking a business rule (as opposed to a technical failure).</summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>A request breaks a business rule, e.g. booking a slot in the past.</summary>
public sealed class BusinessRuleException(string message) : DomainException(message);

/// <summary>A booking clashes with existing bookings: the slot is full or the patient is already booked at that time.</summary>
public sealed class BookingConflictException(string message) : DomainException(message);
