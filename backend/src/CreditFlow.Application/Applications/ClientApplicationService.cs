using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Common;
using CreditFlow.Application.Simulations;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Scoring;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CreditFlow.Application.Applications;

/// <summary>Cas d'usage du client sur ses propres demandes.</summary>
public sealed class ClientApplicationService(
    ICreditApplicationRepository applications,
    ICreditProductRepository products,
    IClientProfileRepository profiles,
    IReferenceGenerator references,
    IUserDirectory users,
    IUnitOfWork unitOfWork,
    IValidator<SaveApplicationRequest> saveValidator,
    IValidator<CancelApplicationRequest> cancelValidator,
    IValidator<ApplicationListQuery> listValidator,
    IOptions<ScoringOptions> scoringOptions,
    TimeProvider clock)
{
    public async Task<ApplicationDetailDto> CreateAsync(Guid clientId, SaveApplicationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await saveValidator.EnsureValidAsync(request, cancellationToken);
        var product = await ProductRules.GetActiveProductAsync(products, request.ProductType!.Value, cancellationToken);
        ProductRules.EnsureWithinBounds(product, request.Amount!.Value, request.DurationMonths!.Value);

        var now = clock.GetUtcNow();
        var reference = await references.NextAsync(now.Year, cancellationToken);
        var application = CreditApplication.CreateDraft(
            reference, clientId, product.Type, request.Amount.Value, request.DurationMonths.Value, request.Purpose, now);
        applications.Add(application);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(application, cancellationToken);
    }

    public async Task<PagedResult<ApplicationSummaryDto>> ListAsync(Guid clientId, ApplicationListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await listValidator.EnsureValidAsync(query, cancellationToken);
        return await applications.ListForClientAsync(clientId, query.Status, query.Page, query.PageSize, cancellationToken);
    }

    public async Task<ApplicationDetailDto> GetAsync(Guid clientId, Guid applicationId, CancellationToken cancellationToken) =>
        await ToDetailAsync(await LoadOwnedAsync(clientId, applicationId, cancellationToken), cancellationToken);

    public async Task<ApplicationDetailDto> UpdateAsync(
        Guid clientId,
        Guid applicationId,
        SaveApplicationRequest request,
        Guid? expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var application = await LoadOwnedAsync(clientId, applicationId, cancellationToken);
        application.EnsureVersion(expectedVersion);
        await saveValidator.EnsureValidAsync(request, cancellationToken);
        var product = await ProductRules.GetActiveProductAsync(products, request.ProductType!.Value, cancellationToken);
        ProductRules.EnsureWithinBounds(product, request.Amount!.Value, request.DurationMonths!.Value);

        application.UpdateTerms(product.Type, request.Amount.Value, request.DurationMonths.Value, request.Purpose, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(application, cancellationToken);
    }

    /// <summary>
    /// Soumission : exige un profil complet, une pièce d'identité et un bulletin de salaire,
    /// puis calcule et fige taux, mensualité, endettement et score.
    /// </summary>
    public async Task<ApplicationDetailDto> SubmitAsync(Guid clientId, Guid applicationId, Guid? expectedVersion, CancellationToken cancellationToken)
    {
        var application = await LoadOwnedAsync(clientId, applicationId, cancellationToken);
        application.EnsureVersion(expectedVersion);
        if (!ApplicationStateMachine.CanApply(application.Status, ApplicationAction.Submit))
        {
            throw new InvalidStatusTransitionException(application.Status, ApplicationAction.Submit);
        }

        var errors = new List<(string Field, string Message)>();
        var profile = await profiles.GetAsync(clientId, cancellationToken);
        if (profile is not { IsComplete: true })
        {
            errors.Add(("profile", "Complétez votre profil financier avant de soumettre la demande."));
        }

        if (!application.HasDocument(DocumentType.IdCard))
        {
            errors.Add(("documents", "Une pièce d'identité (ID_CARD) est obligatoire."));
        }

        if (!application.HasDocument(DocumentType.Payslip))
        {
            errors.Add(("documents", "Un bulletin de salaire (PAYSLIP) est obligatoire."));
        }

        var product = await products.GetAsync(application.ProductType, cancellationToken);
        if (product is null || !product.IsActive)
        {
            errors.Add(("productType", "Ce produit de crédit n'est plus disponible."));
        }
        else
        {
            if (!product.IsAmountAllowed(application.Amount))
            {
                errors.Add(("amount", product.AmountRangeMessage()));
            }

            if (!product.IsDurationAllowed(application.DurationMonths))
            {
                errors.Add(("durationMonths", product.DurationRangeMessage()));
            }
        }

        if (errors.Count > 0)
        {
            throw ValidationExtensions.ValidationErrors(errors);
        }

        var assessment = RiskScoring.Assess(
            new ScoringInput(
                application.ProductType,
                application.Amount,
                application.DurationMonths,
                product!.AnnualRate,
                profile!.MonthlyIncome!.Value,
                profile.MonthlyCharges!.Value,
                profile.EmploymentType!.Value,
                profile.YearsAtCurrentJob!.Value),
            scoringOptions.Value.MaxDebtRatio);

        application.Submit(assessment, clientId, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(application, cancellationToken);
    }

    public async Task<ApplicationDetailDto> CancelAsync(
        Guid clientId,
        Guid applicationId,
        CancelApplicationRequest? request,
        Guid? expectedVersion,
        CancellationToken cancellationToken)
    {
        var cancel = request ?? new CancelApplicationRequest(null);
        await cancelValidator.EnsureValidAsync(cancel, cancellationToken);
        var application = await LoadOwnedAsync(clientId, applicationId, cancellationToken);
        application.EnsureVersion(expectedVersion);
        application.Cancel(clientId, cancel.Comment, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(application, cancellationToken);
    }

    private async Task<CreditApplication> LoadOwnedAsync(Guid clientId, Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await applications.GetAsync(applicationId, cancellationToken);
        return application is not null && application.ClientId == clientId
            ? application
            : throw new NotFoundException("Demande introuvable.");
    }

    private async Task<ApplicationDetailDto> ToDetailAsync(CreditApplication application, CancellationToken cancellationToken)
    {
        var directory = await users.GetAsync(ApplicationMapper.RelatedUserIds(application), cancellationToken);
        return ApplicationMapper.ToDetail(application, directory, includeRisk: false);
    }
}
