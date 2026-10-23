using CreditFlow.Domain.Scoring;

namespace CreditFlow.Application.Common;

/// <summary>Paramètres du scoring (section de configuration <c>Scoring</c>).</summary>
public sealed class ScoringOptions
{
    public const string SectionName = "Scoring";

    /// <summary>Seuil d'endettement au-delà duquel la recommandation est défavorable (0,40 = 40 %).</summary>
    public decimal MaxDebtRatio { get; set; } = RiskScoring.DefaultMaxDebtRatio;
}
