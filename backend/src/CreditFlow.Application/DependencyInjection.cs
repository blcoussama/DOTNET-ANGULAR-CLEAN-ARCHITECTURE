using CreditFlow.Application.Applications;
using CreditFlow.Application.Common;
using CreditFlow.Application.Dashboard;
using CreditFlow.Application.Documents;
using CreditFlow.Application.Products;
using CreditFlow.Application.Profiles;
using CreditFlow.Application.Review;
using CreditFlow.Application.Simulations;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CreditFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<ScoringOptions>()
            .BindConfiguration(ScoringOptions.SectionName)
            .Validate(o => o.MaxDebtRatio is > 0 and <= 1, "Scoring:MaxDebtRatio doit être compris entre 0 et 1.")
            .ValidateOnStart();

        services.AddValidatorsFromAssemblyContaining<SaveApplicationRequestValidator>(ServiceLifetime.Singleton, includeInternalTypes: true);

        services.AddScoped<ProductService>();
        services.AddScoped<SimulationService>();
        services.AddScoped<ClientProfileService>();
        services.AddScoped<ClientApplicationService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
