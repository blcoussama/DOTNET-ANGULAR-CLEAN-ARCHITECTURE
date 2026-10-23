using System.Globalization;
using CreditFlow.Application.Common;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Application.Dashboard;

public sealed class DashboardService(IDashboardDataSource dataSource, TimeProvider clock)
{
    public const int TrendMonths = 6;

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken)
    {
        var counts = await dataSource.CountByStatusAsync(cancellationToken);
        var submitted = await dataSource.GetSubmittedAsync(cancellationToken);
        return Compute(counts, submitted, clock.GetUtcNow());
    }

    internal static DashboardStatsDto Compute(
        IReadOnlyDictionary<ApplicationStatus, int> counts,
        IReadOnlyList<SubmittedApplicationSnapshot> submitted,
        DateTimeOffset now)
    {
        var countsByStatus = Enum.GetValues<ApplicationStatus>()
            .ToDictionary(s => EnumWire.ToWire(s), s => counts.TryGetValue(s, out var c) ? c : 0);

        var approved = submitted.Where(a => a.Status == ApplicationStatus.Approved).ToList();
        var rejectedCount = submitted.Count(a => a.Status == ApplicationStatus.Rejected);
        var decidedCount = approved.Count + rejectedCount;
        var approvalRate = decidedCount == 0 ? 0m : Math.Round((decimal)approved.Count / decidedCount, 4, MidpointRounding.AwayFromZero);

        var averageAmount = submitted.Count == 0 ? 0m : LoanMath(submitted.Average(a => a.Amount));
        var totalApproved = approved.Sum(a => a.Amount);

        var volumeByProduct = Enum.GetValues<ProductType>()
            .Select(p =>
            {
                var items = submitted.Where(a => a.ProductType == p).ToList();
                return new ProductVolumeDto(p, items.Count, items.Sum(a => a.Amount));
            })
            .ToList();

        var durations = submitted
            .Where(a => a.DecidedAt is not null && a.Status is ApplicationStatus.Approved or ApplicationStatus.Rejected)
            .Select(a => (a.DecidedAt!.Value - a.SubmittedAt).TotalDays)
            .ToList();
        var averageDays = durations.Count == 0 ? 0m : Math.Round((decimal)durations.Average(), 1, MidpointRounding.AwayFromZero);

        var firstMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-(TrendMonths - 1));
        var trend = Enumerable.Range(0, TrendMonths)
            .Select(i =>
            {
                var start = firstMonth.AddMonths(i);
                var end = start.AddMonths(1);
                bool InMonth(DateTimeOffset? d) => d is { } value && value >= start && value < end;
                return new MonthlyTrendDto(
                    start.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    submitted.Count(a => InMonth(a.SubmittedAt)),
                    submitted.Count(a => a.Status == ApplicationStatus.Approved && InMonth(a.DecidedAt)),
                    submitted.Count(a => a.Status == ApplicationStatus.Rejected && InMonth(a.DecidedAt)));
            })
            .ToList();

        var grades = Enum.GetValues<RiskGrade>()
            .ToDictionary(g => EnumWire.ToWire(g), g => submitted.Count(a => a.RiskGrade == g));

        return new DashboardStatsDto(countsByStatus, approvalRate, averageAmount, totalApproved, volumeByProduct, averageDays, trend, grades);
    }

    private static decimal LoanMath(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
