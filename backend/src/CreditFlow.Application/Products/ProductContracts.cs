using CreditFlow.Domain.Products;

namespace CreditFlow.Application.Products;

public sealed record ProductDto(
    ProductType Type,
    string Name,
    decimal AnnualRate,
    decimal MinAmount,
    decimal MaxAmount,
    int MinDurationMonths,
    int MaxDurationMonths,
    bool IsActive,
    DateTimeOffset UpdatedAt)
{
    public static ProductDto From(CreditProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);
        return new(
            product.Type,
            product.Name,
            product.AnnualRate,
            product.MinAmount,
            product.MaxAmount,
            product.MinDurationMonths,
            product.MaxDurationMonths,
            product.IsActive,
            product.UpdatedAt);
    }
}

/// <summary>Remplacement complet du paramétrage d'un produit (PUT).</summary>
public sealed record UpdateProductRequest(
    decimal? AnnualRate,
    decimal? MinAmount,
    decimal? MaxAmount,
    int? MinDurationMonths,
    int? MaxDurationMonths,
    bool? IsActive);
