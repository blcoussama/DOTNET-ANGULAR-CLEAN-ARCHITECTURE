using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Common;
using CreditFlow.Domain.Clients;
using FluentValidation;

namespace CreditFlow.Application.Profiles;

public sealed class ClientProfileService(
    IClientProfileRepository profiles,
    IUnitOfWork unitOfWork,
    IValidator<UpdateClientProfileRequest> validator,
    TimeProvider clock)
{
    /// <summary>Profil du client ; 404 tant qu'il n'a jamais été renseigné.</summary>
    public async Task<ClientProfileDto> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        ClientProfileDto.From(await profiles.GetAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Profil financier non renseigné."));

    public async Task<ClientProfileDto> UpdateAsync(Guid userId, UpdateClientProfileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await validator.EnsureValidAsync(request, cancellationToken);

        var now = clock.GetUtcNow();
        var profile = await profiles.GetAsync(userId, cancellationToken);
        if (profile is null)
        {
            profile = ClientProfile.CreateFor(userId, now);
            profiles.Add(profile);
        }

        profile.Update(
            request.DateOfBirth,
            request.Phone,
            request.City,
            request.EmploymentType,
            request.Employer,
            request.MonthlyIncome,
            request.MonthlyCharges,
            request.YearsAtCurrentJob,
            now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ClientProfileDto.From(profile);
    }
}
