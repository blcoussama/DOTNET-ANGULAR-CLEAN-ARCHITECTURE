using CreditFlow.Application.Common;
using CreditFlow.Domain.Applications;
using FluentValidation;

namespace CreditFlow.Application.Review;

public sealed class ReviewQueueQueryValidator : AbstractValidator<ReviewQueueQuery>
{
    public ReviewQueueQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.Search).MaximumLength(100).WithMessage("La recherche est limitée à 100 caractères.");
        RuleFor(x => x.Status)
            .NotEqual(ApplicationStatus.Draft)
            .WithMessage("Les brouillons ne sont pas visibles dans la file d'instruction.");
    }
}

public sealed class DecisionRequestValidator : AbstractValidator<DecisionRequest>
{
    public DecisionRequestValidator()
    {
        RuleFor(x => x.Comment)
            .NotEmpty().WithMessage("Un commentaire est obligatoire.")
            .MaximumLength(CreditApplication.MaxCommentLength)
            .WithMessage($"Le commentaire est limité à {CreditApplication.MaxCommentLength} caractères.");
    }
}
