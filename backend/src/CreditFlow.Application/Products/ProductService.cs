using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Common;
using CreditFlow.Domain.Products;
using FluentValidation;

namespace CreditFlow.Application.Products;

public sealed class ProductService(
    ICreditProductRepository products,
    IUnitOfWork unitOfWork,
    IValidator<UpdateProductRequest> validator,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<ProductDto>> ListActiveAsync(CancellationToken cancellationToken) =>
        [.. (await products.ListAsync(activeOnly: true, cancellationToken)).Select(ProductDto.From)];

    public async Task<IReadOnlyList<ProductDto>> ListAllAsync(CancellationToken cancellationToken) =>
        [.. (await products.ListAsync(activeOnly: false, cancellationToken)).Select(ProductDto.From)];

    public async Task<ProductDto> UpdateAsync(ProductType type, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await validator.EnsureValidAsync(request, cancellationToken);

        var product = await products.GetAsync(type, cancellationToken)
            ?? throw new NotFoundException("Produit de crédit introuvable.");

        product.Update(
            request.AnnualRate!.Value,
            request.MinAmount!.Value,
            request.MaxAmount!.Value,
            request.MinDurationMonths!.Value,
            request.MaxDurationMonths!.Value,
            request.IsActive!.Value,
            clock.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ProductDto.From(product);
    }
}
