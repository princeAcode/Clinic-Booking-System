using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Exceptions;
using ClinicBooking.Application.Mapping;
using ClinicBooking.Contracts.Clinics;
using ClinicBooking.Domain.Entities;
using FluentValidation;

namespace ClinicBooking.Application.Services;

public interface IClinicService
{
    Task<IReadOnlyList<ClinicDto>> GetClinicsAsync(CancellationToken ct = default);
    Task<ClinicDto> GetClinicAsync(Guid id, CancellationToken ct = default);
    Task<ClinicDto> CreateClinicAsync(CreateClinicRequest request, CancellationToken ct = default);
}

public sealed class ClinicService(
    IClinicRepository clinics,
    IUnitOfWork unitOfWork,
    IValidator<CreateClinicRequest> createValidator) : IClinicService
{
    public async Task<IReadOnlyList<ClinicDto>> GetClinicsAsync(CancellationToken ct = default)
    {
        var all = await clinics.GetAllAsync(ct);
        return all.Select(c => c.ToDto()).ToList();
    }

    public async Task<ClinicDto> GetClinicAsync(Guid id, CancellationToken ct = default)
    {
        var clinic = await clinics.GetByIdAsync(id, ct) ?? throw new NotFoundException("Clinic", id);
        return clinic.ToDto();
    }

    public async Task<ClinicDto> CreateClinicAsync(CreateClinicRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var clinic = new Clinic
        {
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            District = request.District.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
        };

        clinics.Add(clinic);
        await unitOfWork.SaveChangesAsync(ct);

        return clinic.ToDto();
    }
}
