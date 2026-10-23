using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Application.Applications;

/// <summary>Création ou modification d'une demande (brouillon / compléments demandés).</summary>
public sealed record SaveApplicationRequest(
    ProductType? ProductType,
    decimal? Amount,
    int? DurationMonths,
    string? Purpose);

public sealed record CancelApplicationRequest(string? Comment);

public sealed record ApplicationListQuery(ApplicationStatus? Status, int Page = 1, int PageSize = 20);

public sealed record ApplicationSummaryDto(
    Guid Id,
    string Reference,
    ProductType ProductType,
    decimal Amount,
    int DurationMonths,
    ApplicationStatus Status,
    decimal? MonthlyPayment,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? DecidedAt);

public sealed record DocumentDto(
    Guid Id,
    DocumentType Type,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt);

public sealed record ApplicationEventDto(
    Guid Id,
    ApplicationStatus? FromStatus,
    ApplicationStatus ToStatus,
    Guid ActorId,
    string ActorRole,
    string? ActorName,
    string? Comment,
    DateTimeOffset OccurredAt);

/// <summary>
/// Détail d'une demande. Pour le client, <c>riskScore</c>, <c>riskGrade</c> et <c>recommendation</c>
/// restent null : le score est un outil interne d'aide à la décision.
/// </summary>
public class ApplicationDetailDto
{
    public Guid Id { get; init; }

    public string Reference { get; init; } = string.Empty;

    public Guid ClientId { get; init; }

    public ProductType ProductType { get; init; }

    public decimal Amount { get; init; }

    public int DurationMonths { get; init; }

    public string? Purpose { get; init; }

    public ApplicationStatus Status { get; init; }

    public decimal? AnnualRate { get; init; }

    public decimal? MonthlyPayment { get; init; }

    public decimal? TotalCost { get; init; }

    public decimal? DebtRatio { get; init; }

    public int? RiskScore { get; init; }

    public RiskGrade? RiskGrade { get; init; }

    public Recommendation? Recommendation { get; init; }

    public Guid? AssignedAnalystId { get; init; }

    public string? AssignedAnalystName { get; init; }

    public string? DecisionComment { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? SubmittedAt { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>Jeton de concurrence à renvoyer (facultatif) dans l'en-tête <c>If-Match</c>.</summary>
    public Guid Version { get; init; }

    public IReadOnlyList<DocumentDto> Documents { get; init; } = [];

    public IReadOnlyList<ApplicationEventDto> Events { get; init; } = [];
}
