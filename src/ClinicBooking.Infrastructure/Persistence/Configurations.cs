using ClinicBooking.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicBooking.Infrastructure.Persistence;

// Slot times are clinic-local wall-clock times, so they are stored without a time zone.
internal static class ColumnTypes
{
    public const string LocalDateTime = "timestamp without time zone";
}

internal sealed class ClinicConfiguration : IEntityTypeConfiguration<Clinic>
{
    public void Configure(EntityTypeBuilder<Clinic> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Address).HasMaxLength(300).IsRequired();
        builder.Property(c => c.District).HasMaxLength(100).IsRequired();
        builder.Property(c => c.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();
    }
}

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.IdNumber).HasMaxLength(20).IsRequired();
        builder.Property(p => p.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(256).IsRequired();
        builder.Ignore(p => p.FullName);

        builder.HasIndex(p => p.IdNumber).IsUnique();
        builder.HasIndex(p => p.UserId).IsUnique();

        builder.HasOne<IdentityUser>()
            .WithOne()
            .HasForeignKey<Patient>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TimeSlotConfiguration : IEntityTypeConfiguration<TimeSlot>
{
    public void Configure(EntityTypeBuilder<TimeSlot> builder)
    {
        builder.Property(s => s.StartTime).HasColumnType(ColumnTypes.LocalDateTime);
        builder.Property(s => s.EndTime).HasColumnType(ColumnTypes.LocalDateTime);

        // Optimistic concurrency: an UPDATE only succeeds if BookedCount is unchanged since it was read,
        // which prevents two simultaneous bookings from both taking the last place.
        builder.Property(s => s.BookedCount).IsConcurrencyToken();

        builder.Ignore(s => s.RemainingCapacity);
        builder.Ignore(s => s.HasAvailability);

        builder.HasOne(s => s.Clinic)
            .WithMany(c => c.TimeSlots)
            .HasForeignKey(s => s.ClinicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.ClinicId, s.StartTime });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_TimeSlots_EndAfterStart", "\"EndTime\" > \"StartTime\"");
            t.HasCheckConstraint("CK_TimeSlots_BookedWithinCapacity", "\"BookedCount\" >= 0 AND \"BookedCount\" <= \"Capacity\"");
        });
    }
}

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.Property(a => a.ReferenceNumber).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(500);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(a => a.IsActive);

        builder.HasIndex(a => a.ReferenceNumber).IsUnique();
        builder.HasIndex(a => new { a.PatientId, a.Status });

        // Database-level guard against the same patient holding the same slot twice.
        builder.HasIndex(a => new { a.PatientId, a.TimeSlotId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Booked'");

        builder.HasOne(a => a.Patient)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.TimeSlot)
            .WithMany()
            .HasForeignKey(a => a.TimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
