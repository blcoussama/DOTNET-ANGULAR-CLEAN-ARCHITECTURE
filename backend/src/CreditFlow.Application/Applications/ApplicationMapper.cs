using CreditFlow.Application.Abstractions;
using CreditFlow.Domain.Applications;

namespace CreditFlow.Application.Applications;

internal static class ApplicationMapper
{
    public static IEnumerable<Guid> RelatedUserIds(CreditApplication application)
    {
        var ids = application.Events.Select(e => e.ActorId).Append(application.ClientId);
        return application.AssignedAnalystId is { } analyst ? ids.Append(analyst).Distinct() : ids.Distinct();
    }

    public static DocumentDto ToDto(ApplicationDocument document) =>
        new(document.Id, document.Type, document.FileName, document.ContentType, document.SizeBytes, document.UploadedAt);

    public static ApplicationDetailDto ToDetail(CreditApplication application, IReadOnlyDictionary<Guid, UserSummary> users, bool includeRisk) =>
        ToDetail<ApplicationDetailDto>(application, users, includeRisk);

    public static T ToDetail<T>(CreditApplication application, IReadOnlyDictionary<Guid, UserSummary> users, bool includeRisk)
        where T : ApplicationDetailDto, new() =>
        new()
        {
            Id = application.Id,
            Reference = application.Reference,
            ClientId = application.ClientId,
            ProductType = application.ProductType,
            Amount = application.Amount,
            DurationMonths = application.DurationMonths,
            Purpose = application.Purpose,
            Status = application.Status,
            AnnualRate = application.AnnualRate,
            MonthlyPayment = application.MonthlyPayment,
            TotalCost = application.TotalCost,
            DebtRatio = application.DebtRatio,
            RiskScore = includeRisk ? application.RiskScore : null,
            RiskGrade = includeRisk ? application.RiskGrade : null,
            Recommendation = includeRisk ? application.Recommendation : null,
            AssignedAnalystId = application.AssignedAnalystId,
            AssignedAnalystName = NameOf(users, application.AssignedAnalystId),
            DecisionComment = application.DecisionComment,
            CreatedAt = application.CreatedAt,
            UpdatedAt = application.UpdatedAt,
            SubmittedAt = application.SubmittedAt,
            DecidedAt = application.DecidedAt,
            Version = application.Version,
            Documents = Documents(application),
            Events = Events(application, users),
        };

    public static IReadOnlyList<DocumentDto> Documents(CreditApplication application) =>
        [.. application.Documents.OrderBy(d => d.UploadedAt).Select(ToDto)];

    public static IReadOnlyList<ApplicationEventDto> Events(CreditApplication application, IReadOnlyDictionary<Guid, UserSummary> users) =>
        [.. application.Events
            .OrderBy(e => e.Sequence)
            .Select(e => new ApplicationEventDto(e.Id, e.FromStatus, e.ToStatus, e.ActorId, e.ActorRole, NameOf(users, e.ActorId), e.Comment, e.OccurredAt))];

    public static string? NameOf(IReadOnlyDictionary<Guid, UserSummary> users, Guid? id) =>
        id is { } key && users.TryGetValue(key, out var user) ? user.FullName : null;
}
