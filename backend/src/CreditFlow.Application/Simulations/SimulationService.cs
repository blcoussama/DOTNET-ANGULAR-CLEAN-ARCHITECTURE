using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Common;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CreditFlow.Application.Simulations;

public sealed class SimulationService(
    ICreditProductRepository products,
    IValidator<SimulationRequest> validator,
    IOptions<ScoringOptions> scoringOptions)
{
    public async Task<SimulationResult> SimulateAsync(SimulationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await validator.EnsureValidAsync(request, cancellationToken);

        var type = request.ProductType!.Value;
        var amount = request.Amount!.Value;
        var duration = request.DurationMonths!.Value;
        var product = await ProductRules.GetActiveProductAsync(products, type, cancellationToken);
        ProductRules.EnsureWithinBounds(product, amount, duration);

        var quote = LoanCalculator.Quote(amount, product.AnnualRate, duration);
        var threshold = scoringOptions.Value.MaxDebtRatio;
        decimal? debtRatio = null;
        bool? within = null;
        if (request.MonthlyIncome is { } income)
        {
            debtRatio = RiskScoring.DebtRatio(request.MonthlyCharges ?? 0m, quote.MonthlyPayment, income);
            within = debtRatio <= threshold;
        }

        return new SimulationResult(type, amount, duration, quote.AnnualRate, quote.MonthlyPayment, quote.TotalCost, debtRatio, within, threshold);
    }
}

/// <summary>Règles communes sur les produits (actif, bornes de montant et de durée).</summary>
internal static class ProductRules
{
    public static async Task<CreditProduct> GetActiveProductAsync(ICreditProductRepository products, ProductType type, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(type, cancellationToken);
        if (product is null || !product.IsActive)
        {
            throw ValidationExtensions.ValidationError("productType", "Ce produit de crédit n'est pas disponible.");
        }

        return product;
    }

    public static void EnsureWithinBounds(CreditProduct product, decimal amount, int durationMonths)
    {
        var errors = new List<(string, string)>();
        if (!product.IsAmountAllowed(amount))
        {
            errors.Add(("amount", product.AmountRangeMessage()));
        }

        if (!product.IsDurationAllowed(durationMonths))
        {
            errors.Add(("durationMonths", product.DurationRangeMessage()));
        }

        if (errors.Count > 0)
        {
            throw ValidationExtensions.ValidationErrors(errors);
        }
    }
}
