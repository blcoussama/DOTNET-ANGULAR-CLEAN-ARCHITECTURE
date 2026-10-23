using FluentValidation;

namespace CreditFlow.Application.Profiles;

public sealed class UpdateClientProfileRequestValidator : AbstractValidator<UpdateClientProfileRequest>
{
    public const int MinAge = 18;
    public const int MaxAge = 100;

    public UpdateClientProfileRequestValidator(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        RuleFor(x => x.DateOfBirth)
            .Must(d => IsAgeWithinBounds(d!.Value, clock))
            .When(x => x.DateOfBirth is not null)
            .WithMessage($"L'âge doit être compris entre {MinAge} et {MaxAge} ans.");

        RuleFor(x => x.Phone)
            .Matches(@"^\+?[0-9][0-9 .\-]{7,19}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Numéro de téléphone invalide (8 à 20 chiffres, espaces, points ou tirets).");

        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage("La ville est limitée à 100 caractères.");

        RuleFor(x => x.Employer)
            .MaximumLength(150).WithMessage("L'employeur est limité à 150 caractères.");

        RuleFor(x => x.EmploymentType)
            .IsInEnum().When(x => x.EmploymentType is not null).WithMessage("Situation professionnelle invalide.");

        RuleFor(x => x.MonthlyIncome)
            .GreaterThan(0m).When(x => x.MonthlyIncome is not null).WithMessage("Le revenu mensuel doit être strictement positif.")
            .LessThanOrEqualTo(10_000_000m).When(x => x.MonthlyIncome is not null).WithMessage("Le revenu mensuel est trop élevé.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).When(x => x.MonthlyIncome is not null).WithMessage("Le revenu accepte au plus 2 décimales.");

        RuleFor(x => x.MonthlyCharges)
            .GreaterThanOrEqualTo(0m).When(x => x.MonthlyCharges is not null).WithMessage("Les charges mensuelles ne peuvent pas être négatives.")
            .LessThanOrEqualTo(10_000_000m).When(x => x.MonthlyCharges is not null).WithMessage("Les charges mensuelles sont trop élevées.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).When(x => x.MonthlyCharges is not null).WithMessage("Les charges acceptent au plus 2 décimales.");

        RuleFor(x => x.YearsAtCurrentJob)
            .InclusiveBetween(0, 60).When(x => x.YearsAtCurrentJob is not null).WithMessage("L'ancienneté doit être comprise entre 0 et 60 ans.");
    }

    private static bool IsAgeWithinBounds(DateOnly dateOfBirth, TimeProvider clock)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age is >= MinAge and <= MaxAge;
    }
}
