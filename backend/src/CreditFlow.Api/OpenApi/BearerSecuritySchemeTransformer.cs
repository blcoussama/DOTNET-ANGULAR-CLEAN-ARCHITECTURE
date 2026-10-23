using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CreditFlow.Api.OpenApi;

/// <summary>Déclare le schéma d'authentification Bearer (JWT) dans le document OpenAPI.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "CreditFlow API";
        document.Info.Description = "Gestion des demandes de crédit : simulation, dépôt, instruction et décision humaine.";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Jeton d'accès obtenu via POST /api/v1/auth/login.",
        };
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        });
        return Task.CompletedTask;
    }
}
