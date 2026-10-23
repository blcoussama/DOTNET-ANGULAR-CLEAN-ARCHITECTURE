using CreditFlow.Application.Common;
using CreditFlow.Domain.Applications;
using FluentValidation;

namespace CreditFlow.Application.Applications;

public sealed class SaveApplicationRequestValidator : AbstractValidator<SaveApplicationRequest>
{
    public SaveApplicationRequestValidator()
    {
        RuleFor(x => x.ProductType)
            .NotNull().WithMessage("Le type de crédit est obligatoire.")
            .IsInEnum().WithMessage("Type de crédit invalide.");
        RuleFor(x => x.Amount)
            .NotNull().WithMessage("Le montant est obligatoire.")
            .GreaterThan(0m).WithMessage("Le montant doit être strictement positif.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Le montant accepte au plus 2 décimales.");
        RuleFor(x => x.DurationMonths)
            .NotNull().WithMessage("La durée est obligatoire.")
            .GreaterThan(0).WithMessage("La durée doit être strictement positive.");
        RuleFor(x => x.Purpose)
            .MaximumLength(CreditApplication.MaxPurposeLength)
            .WithMessage($"L'objet du crédit est limité à {CreditApplication.MaxPurposeLength} caractères.");
    }
}

public sealed class CancelApplicationRequestValidator : AbstractValidator<CancelApplicationRequest>
{
    public CancelApplicationRequestValidator()
    {
        RuleFor(x => x.Comment)
            .MaximumLength(CreditApplication.MaxCommentLength)
            .WithMessage($"Le commentaire est limité à {CreditApplication.MaxCommentLength} caractères.");
    }
}

public sealed class ApplicationListQueryValidator : AbstractValidator<ApplicationListQuery>
{
    public ApplicationListQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}
