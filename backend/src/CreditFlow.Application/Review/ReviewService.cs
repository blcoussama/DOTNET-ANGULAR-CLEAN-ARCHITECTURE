using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Applications;
using CreditFlow.Application.Common;
using CreditFlow.Application.Profiles;
using CreditFlow.Domain.Applications;
using FluentValidation;

namespace CreditFlow.Application.Review;

public enum ReviewDecision
{
    Approve,
    Reject,
    RequestInfo,
}

/// <summary>Instruction des dossiers par les analystes (et administrateurs).</summary>
public sealed class ReviewService(
    ICreditApplicationRepository applications,
    IClientProfileRepository profiles,
    IUserDirectory users,
    IUnitOfWork unitOfWork,
    IValidator<ReviewQueueQuery> queueValidator,
    IValidator<DecisionRequest> decisionValidator,
    TimeProvider clock)
{
    public async Task<PagedResult<ReviewQueueItemDto>> GetQueueAsync(CurrentActor actor, ReviewQueueQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(query);
        await queueValidator.EnsureValidAsync(query, cancellationToken);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var filter = new ReviewQueueFilter(
            query.Page,
            query.PageSize,
            query.Status,
            query.RiskGrade,
            query.ProductType,
            query.AssignedToMe ? actor.UserId : null,
            search,
            query.SortBy,
            query.SortDir);
        return await applications.ListForReviewAsync(filter, cancellationToken);
    }

    public async Task<ReviewApplicationDetailDto> GetAsync(Guid applicationId, CancellationToken cancellationToken) =>
        await ToDetailAsync(await LoadSubmittedAsync(applicationId, cancellationToken), cancellationToken);

    public async Task<ReviewApplicationDetailDto> AssignAsync(CurrentActor actor, Guid applicationId, Guid? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var application = await LoadSubmittedAsync(applicationId, cancellationToken);
        application.EnsureVersion(expectedVersion);
        application.AssignTo(actor.UserId, actor.Role, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(application, cancellationToken);
    }

    public async Task<ReviewApplicationDetailDto> DecideAsync(
        CurrentActor actor,
        Guid applicationId,
        ReviewDecision decision,
        DecisionRequest request,
        Guid? expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var application = await LoadSubmittedAsync(applicationId, cancellationToken);
        application.EnsureVersion(expectedVersion);

        var action = decision switch
        {
            ReviewDecision.Approve => ApplicationAction.Approve,
            ReviewDecision.Reject => ApplicationAction.Reject,
            _ => ApplicationAction.RequestInfo,
        };

        // 409 si la transition est interdite, puis 403 si l'appelant n'est pas l'analyste assigné.
        if (!ApplicationStateMachine.CanApply(application.Status, action))
        {
            throw new InvalidStatusTransitionException(application.Status, action);
        }

        if (!application.CanBeDecidedBy(actor.UserId, actor.Role))
        {
            throw new ForbiddenException("Seul l'analyste assigné à ce dossier (ou un administrateur) peut statuer.");
        }

        await decisionValidator.EnsureValidAsync(request, cancellationToken);
        var now = clock.GetUtcNow();
        var comment = request.Comment!;
        switch (decision)
        {
            case ReviewDecision.Approve:
                application.Approve(actor.UserId, actor.Role, comment, now);
                break;
            case ReviewDecision.Reject:
                application.Reject(actor.UserId, actor.Role, comment, now);
                break;
            default:
                application.RequestInformation(actor.UserId, actor.Role, comment, now);
                break;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(application, cancellationToken);
    }

    /// <summary>Les brouillons jamais soumis restent invisibles des analystes (404).</summary>
    private async Task<CreditApplication> LoadSubmittedAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await applications.GetAsync(applicationId, cancellationToken);
        return application is { HasBeenSubmitted: true }
            ? application
            : throw new NotFoundException("Demande introuvable.");
    }

    private async Task<ReviewApplicationDetailDto> ToDetailAsync(CreditApplication application, CancellationToken cancellationToken)
    {
        var directory = await users.GetAsync(ApplicationMapper.RelatedUserIds(application), cancellationToken);
        var profile = await profiles.GetAsync(application.ClientId, cancellationToken);
        var dto = ApplicationMapper.ToDetail<ReviewApplicationDetailDto>(application, directory, includeRisk: true);
        if (directory.TryGetValue(application.ClientId, out var client))
        {
            dto.Client = new ReviewClientDto(client.Id, client.Email, client.FirstName, client.LastName);
        }

        dto.ClientProfile = profile is null ? null : ClientProfileDto.From(profile);

        var assessment = application.GetAssessment();
        dto.ScoringBreakdown = assessment is null ? null : ScoringBreakdownBuilder.Build(assessment, application.ProductType);
        return dto;
    }
}
