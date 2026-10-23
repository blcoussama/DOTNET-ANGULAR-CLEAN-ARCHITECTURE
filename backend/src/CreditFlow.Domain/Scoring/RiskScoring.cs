using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Common;
using CreditFlow.Domain.Products;

namespace CreditFlow.Domain.Scoring;

/// <summary>Données nécessaires au scoring d'une demande.</summary>
public sealed record ScoringInput(
    ProductType ProductType,
    decimal Amount,
    int DurationMonths,
    decimal AnnualRate,
    decimal MonthlyIncome,
    decimal MonthlyCharges,
    EmploymentType EmploymentType,
    int YearsAtCurrentJob);

/// <summary>Résultat complet et figé du scoring (points par critère inclus).</summary>
public sealed record RiskAssessment(
    decimal AnnualRate,
    decimal MonthlyPayment,
    decimal TotalCost,
    decimal DebtRatio,
    decimal AmountToAnnualIncomeRatio,
    int DebtRatioPoints,
    int EmploymentPoints,
    int SeniorityPoints,
    int AmountToIncomePoints,
    int Score,
    RiskGrade Grade,
    Recommendation Recommendation,
    decimal DebtThreshold,
    decimal MonthlyIncome,
    decimal MonthlyCharges,
    EmploymentType EmploymentType,
    int YearsAtCurrentJob);

/// <summary>
/// Service de domaine pur calculant le score de risque /100 selon le barème de la spécification.
/// Les ratios sont arrondis à 4 décimales avant application des seuils (la valeur affichée est celle notée).
/// </summary>
public static class RiskScoring
{
    public const decimal DefaultMaxDebtRatio = 0.40m;
    public const int MaxDebtRatioPoints = 40;
    public const int MaxEmploymentPoints = 25;
    public const int MaxSeniorityPoints = 15;
    public const int MaxAmountToIncomePoints = 20;

    public static decimal DebtRatio(decimal monthlyCharges, decimal monthlyPayment, decimal monthlyIncome)
    {
        if (monthlyIncome <= 0)
        {
            throw new DomainValidationException("monthlyIncome", "Le revenu mensuel doit être strictement positif.");
        }

        return RoundRatio((monthlyCharges + monthlyPayment) / monthlyIncome);
    }

    public static decimal AmountToAnnualIncomeRatio(decimal amount, decimal monthlyIncome)
    {
        if (monthlyIncome <= 0)
        {
            throw new DomainValidationException("monthlyIncome", "Le revenu mensuel doit être strictement positif.");
        }

        return RoundRatio(amount / (monthlyIncome * 12m));
    }

    public static int DebtRatioPoints(decimal debtRatio) => debtRatio switch
    {
        <= 0.33m => 40,
        <= 0.40m => 25,
        <= 0.50m => 10,
        _ => 0,
    };

    public static int EmploymentPoints(EmploymentType employmentType) => employmentType switch
    {
        EmploymentType.Fonctionnaire => 25,
        EmploymentType.Cdi => 22,
        EmploymentType.Retraite => 18,
        EmploymentType.Cdd => 12,
        EmploymentType.Independant => 10,
        EmploymentType.SansEmploi => 0,
        _ => 0,
    };

    public static int SeniorityPoints(int yearsAtCurrentJob) => yearsAtCurrentJob switch
    {
        >= 5 => 15,
        >= 2 => 10,
        >= 1 => 5,
        _ => 0,
    };

    /// <summary>Montant rapporté au revenu annuel ; seuils multipliés par 4 pour l'immobilier.</summary>
    public static int AmountToIncomePoints(decimal amountToAnnualIncome, ProductType productType)
    {
        var factor = productType == ProductType.Immobilier ? 4m : 1m;
        if (amountToAnnualIncome <= 1m * factor)
        {
            return 20;
        }

        if (amountToAnnualIncome <= 3m * factor)
        {
            return 12;
        }

        return amountToAnnualIncome <= 5m * factor ? 6 : 0;
    }

    public static RiskGrade GradeFor(int score) => score switch
    {
        >= 80 => RiskGrade.A,
        >= 65 => RiskGrade.B,
        >= 50 => RiskGrade.C,
        >= 35 => RiskGrade.D,
        _ => RiskGrade.E,
    };

    public static Recommendation RecommendationFor(RiskGrade grade, decimal debtRatio, decimal maxDebtRatio)
    {
        var withinThreshold = debtRatio <= maxDebtRatio;
        if (!withinThreshold || grade is RiskGrade.D or RiskGrade.E)
        {
            return Recommendation.Defavorable;
        }

        return grade is RiskGrade.A or RiskGrade.B ? Recommendation.Favorable : Recommendation.AEtudier;
    }

    public static RiskAssessment Assess(ScoringInput input, decimal maxDebtRatio = DefaultMaxDebtRatio)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (maxDebtRatio is <= 0 or > 1)
        {
            throw new DomainValidationException(nameof(maxDebtRatio), "Le seuil d'endettement doit être compris entre 0 et 1.");
        }

        if (input.MonthlyCharges < 0)
        {
            throw new DomainValidationException("monthlyCharges", "Les charges mensuelles ne peuvent pas être négatives.");
        }

        var quote = LoanCalculator.Quote(input.Amount, input.AnnualRate, input.DurationMonths);
        var debtRatio = DebtRatio(input.MonthlyCharges, quote.MonthlyPayment, input.MonthlyIncome);
        var amountRatio = AmountToAnnualIncomeRatio(input.Amount, input.MonthlyIncome);

        var debtPoints = DebtRatioPoints(debtRatio);
        var employmentPoints = EmploymentPoints(input.EmploymentType);
        var seniorityPoints = SeniorityPoints(input.YearsAtCurrentJob);
        var amountPoints = AmountToIncomePoints(amountRatio, input.ProductType);
        var score = debtPoints + employmentPoints + seniorityPoints + amountPoints;
        var grade = GradeFor(score);

        return new RiskAssessment(
            quote.AnnualRate,
            quote.MonthlyPayment,
            quote.TotalCost,
            debtRatio,
            amountRatio,
            debtPoints,
            employmentPoints,
            seniorityPoints,
            amountPoints,
            score,
            grade,
            RecommendationFor(grade, debtRatio, maxDebtRatio),
            maxDebtRatio,
            input.MonthlyIncome,
            input.MonthlyCharges,
            input.EmploymentType,
            input.YearsAtCurrentJob);
    }

    private static decimal RoundRatio(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
