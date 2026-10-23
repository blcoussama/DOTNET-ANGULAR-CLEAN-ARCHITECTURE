using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Common;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Domain.Tests;

public sealed class RiskScoringTests
{
    [Theory]
    [InlineData(0.0, 40)]
    [InlineData(0.33, 40)]
    [InlineData(0.3301, 25)]
    [InlineData(0.40, 25)]
    [InlineData(0.4001, 10)]
    [InlineData(0.50, 10)]
    [InlineData(0.5001, 0)]
    [InlineData(1.2, 0)]
    public void Debt_ratio_points_follow_the_thresholds(double ratio, int expected) =>
        RiskScoring.DebtRatioPoints((decimal)ratio).ShouldBe(expected);

    [Theory]
    [InlineData(EmploymentType.Fonctionnaire, 25)]
    [InlineData(EmploymentType.Cdi, 22)]
    [InlineData(EmploymentType.Retraite, 18)]
    [InlineData(EmploymentType.Cdd, 12)]
    [InlineData(EmploymentType.Independant, 10)]
    [InlineData(EmploymentType.SansEmploi, 0)]
    public void Employment_points_follow_the_scale(EmploymentType type, int expected) =>
        RiskScoring.EmploymentPoints(type).ShouldBe(expected);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(4, 10)]
    [InlineData(5, 15)]
    [InlineData(30, 15)]
    public void Seniority_points_follow_the_scale(int years, int expected) =>
        RiskScoring.SeniorityPoints(years).ShouldBe(expected);

    [Theory]
    [InlineData(0.5, 20)]
    [InlineData(1.0, 20)]
    [InlineData(1.0001, 12)]
    [InlineData(3.0, 12)]
    [InlineData(3.01, 6)]
    [InlineData(5.0, 6)]
    [InlineData(5.01, 0)]
    public void Amount_to_income_points_for_consumer_credit(double ratio, int expected) =>
        RiskScoring.AmountToIncomePoints((decimal)ratio, ProductType.Consommation).ShouldBe(expected);

    [Theory]
    [InlineData(4.0, 20)]
    [InlineData(4.01, 12)]
    [InlineData(12.0, 12)]
    [InlineData(12.01, 6)]
    [InlineData(20.0, 6)]
    [InlineData(20.01, 0)]
    public void Amount_to_income_thresholds_are_multiplied_by_four_for_mortgages(double ratio, int expected) =>
        RiskScoring.AmountToIncomePoints((decimal)ratio, ProductType.Immobilier).ShouldBe(expected);

    [Fact]
    public void Auto_loans_use_the_standard_thresholds() =>
        RiskScoring.AmountToIncomePoints(4.0m, ProductType.Auto).ShouldBe(6);

    [Theory]
    [InlineData(100, RiskGrade.A)]
    [InlineData(80, RiskGrade.A)]
    [InlineData(79, RiskGrade.B)]
    [InlineData(65, RiskGrade.B)]
    [InlineData(64, RiskGrade.C)]
    [InlineData(50, RiskGrade.C)]
    [InlineData(49, RiskGrade.D)]
    [InlineData(35, RiskGrade.D)]
    [InlineData(34, RiskGrade.E)]
    [InlineData(0, RiskGrade.E)]
    public void Grade_follows_the_score_thresholds(int score, RiskGrade expected) =>
        RiskScoring.GradeFor(score).ShouldBe(expected);

    [Theory]
    [InlineData(RiskGrade.A, 0.30, 0.40, Recommendation.Favorable)]
    [InlineData(RiskGrade.B, 0.40, 0.40, Recommendation.Favorable)]
    [InlineData(RiskGrade.A, 0.41, 0.40, Recommendation.Defavorable)]
    [InlineData(RiskGrade.C, 0.30, 0.40, Recommendation.AEtudier)]
    [InlineData(RiskGrade.C, 0.45, 0.40, Recommendation.Defavorable)]
    [InlineData(RiskGrade.D, 0.20, 0.40, Recommendation.Defavorable)]
    [InlineData(RiskGrade.E, 0.10, 0.40, Recommendation.Defavorable)]
    [InlineData(RiskGrade.B, 0.45, 0.50, Recommendation.Favorable)]
    public void Recommendation_combines_grade_and_configurable_threshold(RiskGrade grade, double debtRatio, double threshold, Recommendation expected) =>
        RiskScoring.RecommendationFor(grade, (decimal)debtRatio, (decimal)threshold).ShouldBe(expected);

    [Fact]
    public void Debt_ratio_is_rounded_to_four_decimals() =>
        RiskScoring.DebtRatio(1000m, 2003.79m, 10_000m).ShouldBe(0.3004m);

    [Fact]
    public void Debt_ratio_requires_a_positive_income() =>
        Should.Throw<DomainValidationException>(() => RiskScoring.DebtRatio(0m, 100m, 0m)).Field.ShouldBe("monthlyIncome");

    [Fact]
    public void Assess_excellent_profile_is_grade_A_and_favorable()
    {
        // M = 2003,79 ; endettement (1000 + 2003,79) / 10000 = 0,3004 → 40 ; CDI 22 ; 6 ans 15 ; 100000 / 120000 = 0,8333 → 20.
        var result = RiskScoring.Assess(new ScoringInput(ProductType.Consommation, 100_000m, 60, 7.5m, 10_000m, 1_000m, EmploymentType.Cdi, 6));

        result.MonthlyPayment.ShouldBe(2003.79m);
        result.TotalCost.ShouldBe(20227.40m);
        result.DebtRatio.ShouldBe(0.3004m);
        result.AmountToAnnualIncomeRatio.ShouldBe(0.8333m);
        (result.DebtRatioPoints, result.EmploymentPoints, result.SeniorityPoints, result.AmountToIncomePoints).ShouldBe((40, 22, 15, 20));
        result.Score.ShouldBe(97);
        result.Grade.ShouldBe(RiskGrade.A);
        result.Recommendation.ShouldBe(Recommendation.Favorable);
        result.DebtThreshold.ShouldBe(0.40m);
    }

    [Fact]
    public void Assess_mortgage_over_threshold_is_unfavorable_even_with_grade_C()
    {
        // M = 5890,00 ; (1500 + 5890) / 18000 = 0,4106 → 10 ; CDI 22 ; 6 ans 15 ; 900000 / 216000 = 4,1667 ≤ 12 → 12 ⇒ 59 (C).
        var result = RiskScoring.Assess(new ScoringInput(ProductType.Immobilier, 900_000m, 240, 4.9m, 18_000m, 1_500m, EmploymentType.Cdi, 6));

        result.MonthlyPayment.ShouldBe(5890.00m);
        result.DebtRatio.ShouldBe(0.4106m);
        result.Score.ShouldBe(59);
        result.Grade.ShouldBe(RiskGrade.C);
        result.Recommendation.ShouldBe(Recommendation.Defavorable);
    }

    [Fact]
    public void Assess_grade_C_within_threshold_needs_study()
    {
        // M = 1555,31 ; (200 + 1555,31) / 5000 = 0,3511 → 25 ; indépendant 10 ; 1 an 5 ; 0,8333 → 20 ⇒ 60 (C).
        var result = RiskScoring.Assess(new ScoringInput(ProductType.Consommation, 50_000m, 36, 7.5m, 5_000m, 200m, EmploymentType.Independant, 1));

        result.Score.ShouldBe(60);
        result.Grade.ShouldBe(RiskGrade.C);
        result.Recommendation.ShouldBe(Recommendation.AEtudier);
    }

    [Fact]
    public void Assess_weak_profile_is_grade_D()
    {
        // M = 3712,36 ; 3712,36 / 9000 = 0,4125 → 10 ; CDD 12 ; 1 an 5 ; 250000 / 108000 = 2,3148 → 12 ⇒ 39 (D).
        var result = RiskScoring.Assess(new ScoringInput(ProductType.Auto, 250_000m, 84, 6.5m, 9_000m, 0m, EmploymentType.Cdd, 1));

        result.DebtRatio.ShouldBe(0.4125m);
        result.Score.ShouldBe(39);
        result.Grade.ShouldBe(RiskGrade.D);
        result.Recommendation.ShouldBe(Recommendation.Defavorable);
    }

    [Fact]
    public void Assess_uses_the_configured_threshold()
    {
        var input = new ScoringInput(ProductType.Immobilier, 900_000m, 240, 4.9m, 18_000m, 1_500m, EmploymentType.Fonctionnaire, 6);

        RiskScoring.Assess(input, 0.45m).Recommendation.ShouldBe(Recommendation.AEtudier);
        RiskScoring.Assess(input, 0.40m).Recommendation.ShouldBe(Recommendation.Defavorable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    public void Assess_rejects_an_invalid_threshold(double threshold) =>
        Should.Throw<DomainValidationException>(() => RiskScoring.Assess(
            new ScoringInput(ProductType.Auto, 50_000m, 24, 6.5m, 10_000m, 0m, EmploymentType.Cdi, 3), (decimal)threshold));

    [Fact]
    public void Maximum_score_is_100()
    {
        var result = RiskScoring.Assess(new ScoringInput(ProductType.Consommation, 10_000m, 84, 7.5m, 50_000m, 0m, EmploymentType.Fonctionnaire, 10));
        result.Score.ShouldBe(100);
    }
}
