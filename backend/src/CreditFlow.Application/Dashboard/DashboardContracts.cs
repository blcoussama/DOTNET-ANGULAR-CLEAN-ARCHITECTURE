using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Application.Dashboard;

public sealed record ProductVolumeDto(ProductType ProductType, int Count, decimal TotalAmount);

/// <summary>Activité mensuelle ; <c>month</c> au format AAAA-MM (UTC).</summary>
public sealed record MonthlyTrendDto(string Month, int Submitted, int Approved, int Rejected);

/// <summary>
/// Indicateurs du tableau de bord. <c>approvalRate</c> est un ratio 0–1 (approuvées / décidées) ;
/// les montants portent sur les demandes soumises au moins une fois.
/// </summary>
public sealed record DashboardStatsDto(
    IReadOnlyDictionary<string, int> CountsByStatus,
    decimal ApprovalRate,
    decimal AverageAmount,
    decimal TotalApprovedAmount,
    IReadOnlyList<ProductVolumeDto> VolumeByProduct,
    decimal AverageProcessingDays,
    IReadOnlyList<MonthlyTrendDto> MonthlyTrend,
    IReadOnlyDictionary<string, int> GradeDistribution);

/// <summary>Projection minimale d'une demande soumise, utilisée pour les agrégats.</summary>
public sealed record SubmittedApplicationSnapshot(
    ApplicationStatus Status,
    ProductType ProductType,
    decimal Amount,
    RiskGrade? RiskGrade,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt);

public interface IDashboardDataSource
{
    Task<IReadOnlyDictionary<ApplicationStatus, int>> CountByStatusAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SubmittedApplicationSnapshot>> GetSubmittedAsync(CancellationToken cancellationToken);
}
