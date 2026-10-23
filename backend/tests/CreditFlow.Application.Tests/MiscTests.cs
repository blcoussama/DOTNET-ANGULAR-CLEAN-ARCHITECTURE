using CreditFlow.Application.Common;
using CreditFlow.Application.Dashboard;
using CreditFlow.Application.Review;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Application.Tests;

public sealed class EnumWireTests
{
    [Theory]
    [InlineData(ApplicationStatus.UnderReview, "UNDER_REVIEW")]
    [InlineData(ApplicationStatus.InfoRequested, "INFO_REQUESTED")]
    [InlineData(ApplicationStatus.Draft, "DRAFT")]
    public void Statuses_use_upper_snake_case(ApplicationStatus status, string expected) => EnumWire.ToWire(status).ShouldBe(expected);

    [Fact]
    public void Wire_names_match_the_specification()
    {
        Enum.GetValues<EmploymentType>().Select(e => EnumWire.ToWire(e))
            .ShouldBe(["CDI", "CDD", "FONCTIONNAIRE", "INDEPENDANT", "RETRAITE", "SANS_EMPLOI"]);
        Enum.GetValues<DocumentType>().Select(e => EnumWire.ToWire(e)).ShouldBe(["ID_CARD", "PAYSLIP", "BANK_STATEMENT", "OTHER"]);
        Enum.GetValues<Recommendation>().Select(e => EnumWire.ToWire(e)).ShouldBe(["FAVORABLE", "A_ETUDIER", "DEFAVORABLE"]);
        Enum.GetValues<ProductType>().Select(e => EnumWire.ToWire(e)).ShouldBe(["CONSOMMATION", "AUTO", "IMMOBILIER"]);
        Enum.GetValues<RiskGrade>().Select(e => EnumWire.ToWire(e)).ShouldBe(["A", "B", "C", "D", "E"]);
    }

    [Theory]
    [InlineData("UNDER_REVIEW", true)]
    [InlineData("under_review", true)]
    [InlineData("UnderReview", true)]
    [InlineData("2", false)]
    [InlineData("REVIEW", false)]
    [InlineData("", false)]
    public void Parsing_accepts_wire_and_csharp_names_but_not_numbers(string text, bool ok)
    {
        EnumWire.TryParse<ApplicationStatus>(text, out var value).ShouldBe(ok);
        if (ok)
        {
            value.ShouldBe(ApplicationStatus.UnderReview);
        }
    }
}

public sealed class DashboardServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Compute_aggregates_decisions_amounts_and_trend()
    {
        var counts = new Dictionary<ApplicationStatus, int> { [ApplicationStatus.Draft] = 2, [ApplicationStatus.Approved] = 2, [ApplicationStatus.Rejected] = 1 };
        var rows = new List<SubmittedApplicationSnapshot>
        {
            new(ApplicationStatus.Approved, ProductType.Consommation, 10_000m, RiskGrade.A, Now.AddDays(-10), Now.AddDays(-8)),
            new(ApplicationStatus.Approved, ProductType.Auto, 50_000m, RiskGrade.B, Now.AddDays(-40), Now.AddDays(-36)),
            new(ApplicationStatus.Rejected, ProductType.Consommation, 30_000m, RiskGrade.E, Now.AddDays(-5), Now.AddDays(-2)),
            new(ApplicationStatus.Submitted, ProductType.Immobilier, 1_000_000m, RiskGrade.C, Now.AddDays(-1), null),
        };

        var stats = DashboardService.Compute(counts, rows, Now);

        stats.CountsByStatus["DRAFT"].ShouldBe(2);
        stats.CountsByStatus["UNDER_REVIEW"].ShouldBe(0);
        stats.CountsByStatus.Count.ShouldBe(7);
        stats.ApprovalRate.ShouldBe(0.6667m);
        stats.AverageAmount.ShouldBe(272_500m);
        stats.TotalApprovedAmount.ShouldBe(60_000m);
        stats.AverageProcessingDays.ShouldBe(3m);
        stats.VolumeByProduct.Single(v => v.ProductType == ProductType.Consommation).ShouldBe(new ProductVolumeDto(ProductType.Consommation, 2, 40_000m));
        stats.GradeDistribution.ShouldBe(new Dictionary<string, int> { ["A"] = 1, ["B"] = 1, ["C"] = 1, ["D"] = 0, ["E"] = 1 });
        stats.MonthlyTrend.Count.ShouldBe(6);
        stats.MonthlyTrend[0].Month.ShouldBe("2026-05");
        stats.MonthlyTrend[^1].ShouldBe(new MonthlyTrendDto("2026-10", 3, 1, 1));
        stats.MonthlyTrend[^2].ShouldBe(new MonthlyTrendDto("2026-09", 1, 1, 0));
    }

    [Fact]
    public void Compute_handles_an_empty_portfolio()
    {
        var stats = DashboardService.Compute(new Dictionary<ApplicationStatus, int>(), [], Now);

        stats.ApprovalRate.ShouldBe(0m);
        stats.AverageAmount.ShouldBe(0m);
        stats.AverageProcessingDays.ShouldBe(0m);
        stats.MonthlyTrend.ShouldAllBe(m => m.Submitted == 0);
    }
}

public sealed class ScoringBreakdownBuilderTests
{
    [Fact]
    public void Mortgage_breakdown_mentions_the_multiplied_thresholds()
    {
        var assessment = RiskScoring.Assess(new ScoringInput(ProductType.Immobilier, 900_000m, 240, 4.9m, 18_000m, 1_500m, EmploymentType.Cdi, 6));

        var breakdown = ScoringBreakdownBuilder.Build(assessment, ProductType.Immobilier);

        breakdown.WithinDebtThreshold.ShouldBeFalse();
        breakdown.Criteria.Single(c => c.Criterion == "AMOUNT_TO_INCOME").Detail.ShouldContain("≤ 12");
        breakdown.Criteria.Single(c => c.Criterion == "EMPLOYMENT_STABILITY").Detail.ShouldContain("CDI");
        breakdown.Criteria.Sum(c => c.MaxPoints).ShouldBe(100);
        breakdown.Inputs.MonthlyPayment.ShouldBe(5890.00m);
        breakdown.Criteria[0].Detail.ShouldStartWith("41,06 %");
    }
}
