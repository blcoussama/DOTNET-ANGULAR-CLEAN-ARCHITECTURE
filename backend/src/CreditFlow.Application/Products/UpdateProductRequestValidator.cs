using CreditFlow.Domain.Products;
using FluentValidation;

namespace CreditFlow.Application.Products;

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.AnnualRate)
            .NotNull().WithMessage("Le taux annuel est obligatoire.")
            .InclusiveBetween(0m, CreditProduct.MaxAnnualRate).WithMessage($"Le taux annuel doit être compris entre 0 et {CreditProduct.MaxAnnualRate} %.")
            .PrecisionScale(6, 3, ignoreTrailingZeros: true).WithMessage("Le taux annuel accepte au plus 3 décimales.");

        RuleFor(x => x.MinAmount)
            .NotNull().WithMessage("Le montant minimal est obligatoire.")
            .GreaterThan(0m).WithMessage("Le montant minimal doit être strictement positif.")
            .LessThanOrEqualTo(100_000_000m).WithMessage("Le montant minimal est trop élevé.");

        RuleFor(x => x.MaxAmount)
            .NotNull().WithMessage("Le montant maximal est obligatoire.")
            .LessThanOrEqualTo(100_000_000m).WithMessage("Le montant maximal est trop élevé.")
            .GreaterThanOrEqualTo(x => x.MinAmount ?? 0m).WithMessage("Le montant maximal doit être supérieur ou égal au montant minimal.");

        RuleFor(x => x.MinDurationMonths)
            .NotNull().WithMessage("La durée minimale est obligatoire.")
            .GreaterThanOrEqualTo(1).WithMessage("La durée minimale doit être d'au moins 1 mois.");

        RuleFor(x => x.MaxDurationMonths)
            .NotNull().WithMessage("La durée maximale est obligatoire.")
            .LessThanOrEqualTo(CreditProduct.MaxDurationLimit).WithMessage($"La durée maximale ne peut pas dépasser {CreditProduct.MaxDurationLimit} mois.")
            .GreaterThanOrEqualTo(x => x.MinDurationMonths ?? 0).WithMessage("La durée maximale doit être supérieure ou égale à la durée minimale.");

        RuleFor(x => x.IsActive).NotNull().WithMessage("L'indicateur d'activation est obligatoire.");
    }
}
