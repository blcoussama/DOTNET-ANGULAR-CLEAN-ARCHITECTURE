using CreditFlow.Application.Common;
using CreditFlow.Domain.Common;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Application.Review;

/// <summary>Rend lisible, critère par critère, le score figé d'une demande.</summary>
public static class ScoringBreakdownBuilder
{
    private static readonly IFormatProvider French = FrenchFormat.Numbers;

    public static ScoringBreakdownDto Build(RiskAssessment assessment, ProductType productType)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        var factor = productType == ProductType.Immobilier ? 4 : 1;
        var criteria = new List<ScoringCriterionDto>
        {
            new(
                "DEBT_RATIO",
                "Taux d'endettement",
                assessment.DebtRatioPoints,
                RiskScoring.MaxDebtRatioPoints,
                string.Create(French, $"{assessment.DebtRatio:P2} après crédit (≤ 33 % : 40 pts, ≤ 40 % : 25, ≤ 50 % : 10)")),
            new(
                "EMPLOYMENT_STABILITY",
                "Stabilité de l'emploi",
                assessment.EmploymentPoints,
                RiskScoring.MaxEmploymentPoints,
                $"Situation : {EnumWire.ToWire(assessment.EmploymentType)}"),
            new(
                "SENIORITY",
                "Ancienneté dans l'emploi",
                assessment.SeniorityPoints,
                RiskScoring.MaxSeniorityPoints,
                string.Create(French, $"{assessment.YearsAtCurrentJob} an(s) (≥ 5 : 15 pts, ≥ 2 : 10, ≥ 1 : 5)")),
            new(
                "AMOUNT_TO_INCOME",
                "Montant rapporté au revenu annuel",
                assessment.AmountToIncomePoints,
                RiskScoring.MaxAmountToIncomePoints,
                string.Create(French, $"{assessment.AmountToAnnualIncomeRatio:N2} × le revenu annuel (≤ {1 * factor} : 20 pts, ≤ {3 * factor} : 12, ≤ {5 * factor} : 6)")),
        };

        return new ScoringBreakdownDto(
            assessment.DebtRatioPoints,
            assessment.EmploymentPoints,
            assessment.SeniorityPoints,
            assessment.AmountToIncomePoints,
            assessment.Score,
            RiskScoring.MaxDebtRatioPoints + RiskScoring.MaxEmploymentPoints + RiskScoring.MaxSeniorityPoints + RiskScoring.MaxAmountToIncomePoints,
            assessment.Grade,
            assessment.Recommendation,
            assessment.DebtRatio,
            assessment.DebtThreshold,
            assessment.DebtRatio <= assessment.DebtThreshold,
            assessment.AmountToAnnualIncomeRatio,
            criteria,
            new ScoringInputsDto(
                assessment.MonthlyIncome,
                assessment.MonthlyCharges,
                assessment.EmploymentType,
                assessment.YearsAtCurrentJob,
                assessment.AnnualRate,
                assessment.MonthlyPayment));
    }
}
