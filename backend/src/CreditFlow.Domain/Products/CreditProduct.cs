using System.Globalization;
using CreditFlow.Domain.Common;

namespace CreditFlow.Domain.Products;

/// <summary>Produit de crédit paramétrable par l'administrateur.</summary>
public sealed class CreditProduct
{
    public const decimal MaxAnnualRate = 30m;
    public const int MaxDurationLimit = 480;

    private CreditProduct()
    {
    }

    public ProductType Type { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Taux annuel nominal en pourcentage (ex. 7,5 pour 7,5 %).</summary>
    public decimal AnnualRate { get; private set; }

    public decimal MinAmount { get; private set; }

    public decimal MaxAmount { get; private set; }

    public int MinDurationMonths { get; private set; }

    public int MaxDurationMonths { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static CreditProduct Create(
        ProductType type,
        string name,
        decimal annualRate,
        decimal minAmount,
        decimal maxAmount,
        int minDurationMonths,
        int maxDurationMonths,
        bool isActive,
        DateTimeOffset now)
    {
        var product = new CreditProduct { Type = type, Name = name };
        product.Update(annualRate, minAmount, maxAmount, minDurationMonths, maxDurationMonths, isActive, now);
        return product;
    }

    /// <summary>Paramétrage initial des trois produits (données de référence).</summary>
    public static IReadOnlyList<CreditProduct> Defaults(DateTimeOffset now) =>
    [
        Create(ProductType.Consommation, "Crédit consommation", 7.5m, 5_000m, 300_000m, 6, 84, true, now),
        Create(ProductType.Auto, "Crédit auto", 6.5m, 20_000m, 600_000m, 12, 84, true, now),
        Create(ProductType.Immobilier, "Crédit immobilier", 4.9m, 100_000m, 5_000_000m, 60, 300, true, now),
    ];

    public void Update(
        decimal annualRate,
        decimal minAmount,
        decimal maxAmount,
        int minDurationMonths,
        int maxDurationMonths,
        bool isActive,
        DateTimeOffset now)
    {
        if (annualRate is < 0 or > MaxAnnualRate)
        {
            throw new DomainValidationException(nameof(annualRate), $"Le taux annuel doit être compris entre 0 et {MaxAnnualRate} %.");
        }

        if (minAmount <= 0)
        {
            throw new DomainValidationException(nameof(minAmount), "Le montant minimal doit être strictement positif.");
        }

        if (maxAmount < minAmount)
        {
            throw new DomainValidationException(nameof(maxAmount), "Le montant maximal doit être supérieur ou égal au montant minimal.");
        }

        if (minDurationMonths < 1)
        {
            throw new DomainValidationException(nameof(minDurationMonths), "La durée minimale doit être d'au moins 1 mois.");
        }

        if (maxDurationMonths < minDurationMonths || maxDurationMonths > MaxDurationLimit)
        {
            throw new DomainValidationException(nameof(maxDurationMonths), $"La durée maximale doit être comprise entre la durée minimale et {MaxDurationLimit} mois.");
        }

        AnnualRate = annualRate;
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        MinDurationMonths = minDurationMonths;
        MaxDurationMonths = maxDurationMonths;
        IsActive = isActive;
        UpdatedAt = now;
    }

    public bool IsAmountAllowed(decimal amount) => amount >= MinAmount && amount <= MaxAmount;

    public bool IsDurationAllowed(int durationMonths) =>
        durationMonths >= MinDurationMonths && durationMonths <= MaxDurationMonths;

    public string AmountRangeMessage() =>
        string.Create(
            FrenchFormat.Numbers,
            $"Le montant doit être compris entre {MinAmount:N0} et {MaxAmount:N0} MAD pour ce produit.");

    public string DurationRangeMessage() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"La durée doit être comprise entre {MinDurationMonths} et {MaxDurationMonths} mois pour ce produit.");
}
