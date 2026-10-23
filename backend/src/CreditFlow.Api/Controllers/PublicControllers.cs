using CreditFlow.Api.Configuration;
using CreditFlow.Api.Http;
using CreditFlow.Application.Products;
using CreditFlow.Application.Profiles;
using CreditFlow.Application.Simulations;
using CreditFlow.Domain.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditFlow.Api.Controllers;

[ApiController]
[Route("api/v1/simulations")]
[AllowAnonymous]
public sealed class SimulationsController(SimulationService simulations) : ControllerBase
{
    /// <summary>Simulation publique : mensualité, coût total et, si le revenu est fourni, taux d'endettement.</summary>
    [HttpPost]
    [ProducesResponseType<SimulationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<SimulationResult> Simulate(SimulationRequest request, CancellationToken cancellationToken) =>
        simulations.SimulateAsync(request, cancellationToken);
}

[ApiController]
[Route("api/v1/products")]
[AllowAnonymous]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    /// <summary>Produits de crédit actifs.</summary>
    [HttpGet]
    public Task<IReadOnlyList<ProductDto>> List(CancellationToken cancellationToken) => products.ListActiveAsync(cancellationToken);
}

[ApiController]
[Route("api/v1/admin/products")]
[Authorize(Policy = Policies.Admin)]
public sealed class AdminProductsController(ProductService products) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProductDto>> List(CancellationToken cancellationToken) => products.ListAllAsync(cancellationToken);

    /// <summary>Remplace le paramétrage du produit (CONSOMMATION, AUTO ou IMMOBILIER).</summary>
    [HttpPut("{type}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ProductDto> Update(string type, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var values = new QueryValues();
        var productType = values.Enum<ProductType>(type, "type");
        values.ThrowIfInvalid();
        return products.UpdateAsync(productType!.Value, request, cancellationToken);
    }
}

[ApiController]
[Route("api/v1/clients/me/profile")]
[Authorize(Policy = Policies.Client)]
public sealed class ClientProfileController(ClientProfileService profiles) : ControllerBase
{
    /// <summary>Profil financier (vide et incomplet tant qu'il n'a pas été renseigné).</summary>
    [HttpGet]
    public Task<ClientProfileDto> Get(CancellationToken cancellationToken) => profiles.GetAsync(User.UserId(), cancellationToken);

    [HttpPut]
    [ProducesResponseType<ClientProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<ClientProfileDto> Update(UpdateClientProfileRequest request, CancellationToken cancellationToken) =>
        profiles.UpdateAsync(User.UserId(), request, cancellationToken);
}
