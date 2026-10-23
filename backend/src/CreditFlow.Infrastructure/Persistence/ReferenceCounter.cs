namespace CreditFlow.Infrastructure.Persistence;

/// <summary>Compteur annuel servant à générer les références CF-AAAA-NNNNNN.</summary>
public sealed class ReferenceCounter
{
    public int Year { get; set; }

    public int LastValue { get; set; }
}
