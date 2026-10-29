namespace ClinicBooking.Domain.Entities;

/// <summary>
/// A person who receives care and books appointments. Linked one-to-one with an identity user account.
/// </summary>
public sealed class Patient
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identifier of the login account (ASP.NET Identity user) that owns this patient record.</summary>
    public required string UserId { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    /// <summary>South African ID number or passport number. Unique per patient.</summary>
    public required string IdNumber { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public required string PhoneNumber { get; set; }

    public required string Email { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public string FullName => $"{FirstName} {LastName}";
}
