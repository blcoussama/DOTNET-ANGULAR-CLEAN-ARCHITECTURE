namespace CreditFlow.Domain.Scoring;

/// <summary>Recommandation indicative (sérialisée FAVORABLE, A_ETUDIER, DEFAVORABLE). La décision reste humaine.</summary>
public enum Recommendation
{
    Favorable,
    AEtudier,
    Defavorable,
}
