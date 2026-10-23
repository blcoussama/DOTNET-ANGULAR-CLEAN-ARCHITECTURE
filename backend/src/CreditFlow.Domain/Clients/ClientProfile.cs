using CreditFlow.Domain.Common;

namespace CreditFlow.Domain.Clients;

/// <summary>Profil financier d'un client (relation 1–1 avec son compte).</summary>
public sealed class ClientProfile
{
    private ClientProfile()
    {
    }

    public Guid UserId { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public string? Phone { get; private set; }

    public string? City { get; private set; }

    public EmploymentType? EmploymentType { get; private set; }

    public string? Employer { get; private set; }

    public decimal? MonthlyIncome { get; private set; }

    public decimal? MonthlyCharges { get; private set; }

    public int? YearsAtCurrentJob { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Tous les champs nécessaires au scoring sont renseignés (l'employeur reste facultatif).</summary>
    public bool IsComplete =>
        DateOfBirth is not null
        && !string.IsNullOrWhiteSpace(Phone)
        && !string.IsNullOrWhiteSpace(City)
        && EmploymentType is not null
        && MonthlyIncome is > 0
        && MonthlyCharges is >= 0
        && YearsAtCurrentJob is >= 0;

    public static ClientProfile CreateFor(Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(userId), "Utilisateur obligatoire.");
        }

        return new ClientProfile { UserId = userId, UpdatedAt = now };
    }

    public void Update(
        DateOnly? dateOfBirth,
        string? phone,
        string? city,
        EmploymentType? employmentType,
        string? employer,
        decimal? monthlyIncome,
        decimal? monthlyCharges,
        int? yearsAtCurrentJob,
        DateTimeOffset now)
    {
        if (monthlyIncome is <= 0)
        {
            throw new DomainValidationException("monthlyIncome", "Le revenu mensuel doit être strictement positif.");
        }

        if (monthlyCharges is < 0)
        {
            throw new DomainValidationException("monthlyCharges", "Les charges mensuelles ne peuvent pas être négatives.");
        }

        if (yearsAtCurrentJob is < 0)
        {
            throw new DomainValidationException("yearsAtCurrentJob", "L'ancienneté ne peut pas être négative.");
        }

        DateOfBirth = dateOfBirth;
        Phone = Normalize(phone);
        City = Normalize(city);
        EmploymentType = employmentType;
        Employer = Normalize(employer);
        MonthlyIncome = monthlyIncome;
        MonthlyCharges = monthlyCharges;
        YearsAtCurrentJob = yearsAtCurrentJob;
        UpdatedAt = now;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
