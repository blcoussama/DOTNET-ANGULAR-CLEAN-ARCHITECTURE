using CreditFlow.Domain.Products;
using FluentValidation;

namespace CreditFlow.Application.Simulations;

public sealed record SimulationRequest(
    ProductType? ProductType,
    decimal? Amount,
    int? DurationMonths,
    decimal? MonthlyIncome,
    decimal? MonthlyCharges);

/// <summary>Résultat d'une simulation ; <c>debtRatio</c> et <c>withinDebtThreshold</c> seulement si le revenu est fourni.</summary>
public sealed record SimulationResult(
    ProductType ProductType,
    decimal Amount,
    int DurationMonths,
    decimal AnnualRate,
    decimal MonthlyPayment,
    decimal TotalCost,
    decimal? DebtRatio,
    bool? WithinDebtThreshold,
    decimal DebtThreshold);

public sealed class SimulationRequestValidator : AbstractValidator<SimulationRequest>
{
    public SimulationRequestValidator()
    {
        RuleFor(x => x.ProductType).NotNull().WithMessage("Le type de crédit est obligatoire.");
        RuleFor(x => x.Amount)
            .NotNull().WithMessage("Le montant est obligatoire.")
            .GreaterThan(0m).WithMessage("Le montant doit être strictement positif.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Le montant accepte au plus 2 décimales.");
        RuleFor(x => x.DurationMonths)
            .NotNull().WithMessage("La durée est obligatoire.")
            .GreaterThan(0).WithMessage("La durée doit être strictement positive.");
        RuleFor(x => x.MonthlyIncome)
            .GreaterThan(0m).When(x => x.MonthlyIncome is not null).WithMessage("Le revenu mensuel doit être strictement positif.")
            .LessThanOrEqualTo(10_000_000m).When(x => x.MonthlyIncome is not null).WithMessage("Le revenu mensuel est trop élevé.");
        RuleFor(x => x.MonthlyCharges)
            .GreaterThanOrEqualTo(0m).When(x => x.MonthlyCharges is not null).WithMessage("Les charges mensuelles ne peuvent pas être négatives.")
            .LessThanOrEqualTo(10_000_000m).When(x => x.MonthlyCharges is not null).WithMessage("Les charges mensuelles sont trop élevées.");
    }
}
