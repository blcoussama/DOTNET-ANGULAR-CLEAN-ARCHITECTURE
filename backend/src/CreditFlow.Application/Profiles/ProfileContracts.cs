using CreditFlow.Domain.Clients;

namespace CreditFlow.Application.Profiles;

public sealed record ClientProfileDto(
    DateOnly? DateOfBirth,
    string? Phone,
    string? City,
    EmploymentType? EmploymentType,
    string? Employer,
    decimal? MonthlyIncome,
    decimal? MonthlyCharges,
    int? YearsAtCurrentJob,
    bool IsComplete,
    DateTimeOffset UpdatedAt)
{
    public static ClientProfileDto From(ClientProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new(
            profile.DateOfBirth,
            profile.Phone,
            profile.City,
            profile.EmploymentType,
            profile.Employer,
            profile.MonthlyIncome,
            profile.MonthlyCharges,
            profile.YearsAtCurrentJob,
            profile.IsComplete,
            profile.UpdatedAt);
    }
}

/// <summary>Remplacement du profil (PUT) ; les champs absents sont effacés et rendent le profil incomplet.</summary>
public sealed record UpdateClientProfileRequest(
    DateOnly? DateOfBirth,
    string? Phone,
    string? City,
    EmploymentType? EmploymentType,
    string? Employer,
    decimal? MonthlyIncome,
    decimal? MonthlyCharges,
    int? YearsAtCurrentJob);
