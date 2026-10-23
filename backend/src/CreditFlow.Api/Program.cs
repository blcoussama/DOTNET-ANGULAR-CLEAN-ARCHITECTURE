using CreditFlow.Api.Configuration;
using CreditFlow.Application;
using CreditFlow.Infrastructure;
using CreditFlow.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize = ApiServiceCollectionExtensions.MaxRequestBodyBytes;
});

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(services.GetRequiredService<IConfiguration>())
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi();

var app = builder.Build();

// Refus de démarrer si la configuration est invalide (clé JWT absente ou trop courte, fournisseur inconnu…),
// avant toute migration ou initialisation de données.
app.Services.GetService<IStartupValidator>()?.Validate();
app.UseApi();

await app.Services.InitializeDatabaseAsync(app.Environment.IsDevelopment(), app.Lifetime.ApplicationStopping);
await app.RunAsync();

/// <summary>Point d'entrée exposé pour les tests d'intégration (WebApplicationFactory).</summary>
public partial class Program;
