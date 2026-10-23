using CreditFlow.Application.Applications;
using CreditFlow.Application.Profiles;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Application.Review;

public enum ReviewSortField
{
    SubmittedAt,
    Amount,
    RiskScore,
}

public enum SortDirection
{
    Asc,
    Desc,
}

/// <summary>Paramètres de la file d'instruction (tous facultatifs).</summary>
public sealed record ReviewQueueQuery(
    int Page = 1,
    int PageSize = 20,
    ApplicationStatus? Status = null,
    RiskGrade? RiskGrade = null,
    ProductType? ProductType = null,
    bool AssignedToMe = false,
    string? Search = null,
    ReviewSortField SortBy = ReviewSortField.SubmittedAt,
    SortDirection SortDir = SortDirection.Asc);

/// <summary>Filtre résolu transmis au dépôt (inclut l'utilisateur courant).</summary>
public sealed record ReviewQueueFilter(
    int Page,
    int PageSize,
    ApplicationStatus? Status,
    RiskGrade? RiskGrade,
    ProductType? ProductType,
    Guid? AssignedTo,
    string? Search,
    ReviewSortField SortBy,
    SortDirection SortDir);

public sealed record ReviewQueueItemDto(
    Guid Id,
    string Reference,
    Guid ClientId,
    string ClientName,
    ProductType ProductType,
    decimal Amount,
    int DurationMonths,
    ApplicationStatus Status,
    decimal? MonthlyPayment,
    decimal? DebtRatio,
    int? RiskScore,
    RiskGrade? RiskGrade,
    Recommendation? Recommendation,
    Guid? AssignedAnalystId,
    string? AssignedAnalystName,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset UpdatedAt);

public sealed record DecisionRequest(string? Comment);

public sealed record ReviewClientDto(Guid Id, string Email, string FirstName, string LastName);

public sealed record ScoringCriterionDto(string Criterion, string Label, int Points, int MaxPoints, string Detail);

public sealed record ScoringInputsDto(
    decimal MonthlyIncome,
    decimal MonthlyCharges,
    EmploymentType EmploymentType,
    int YearsAtCurrentJob,
    decimal AnnualRate,
    decimal MonthlyPayment);

/// <summary>Décomposition du score figé à la soumission (points par critère, puis détail lisible dans <c>criteria</c>).</summary>
public sealed record ScoringBreakdownDto(
    int DebtRatioPoints,
    int EmploymentPoints,
    int SeniorityPoints,
    int AmountToIncomePoints,
    int Total,
    int MaxScore,
    RiskGrade Grade,
    Recommendation Recommendation,
    decimal DebtRatio,
    decimal DebtThreshold,
    bool WithinDebtThreshold,
    decimal AmountToAnnualIncomeRatio,
    IReadOnlyList<ScoringCriterionDto> Criteria,
    ScoringInputsDto Inputs);

/// <summary>Dossier complet vu par l'analyste : détail + client + profil financier actuel + décomposition du score.</summary>
public sealed class ReviewApplicationDetailDto : ApplicationDetailDto
{
    public ReviewClientDto? Client { get; set; }

    public ClientProfileDto? ClientProfile { get; set; }

    public ScoringBreakdownDto? ScoringBreakdown { get; set; }
}
