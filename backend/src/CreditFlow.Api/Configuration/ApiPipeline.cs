using System.Text.Json;
using CreditFlow.Api.Http;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;

namespace CreditFlow.Api.Configuration;

internal static class ApiPipeline
{
    public static WebApplication UseApi(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
        {
            app.UseForwardedHeaders();
        }

        // Journal des requêtes en premier : il voit le code HTTP final produit par le gestionnaire d'exceptions.
        app.UseSerilogRequestLogging(options =>
        {
            // Chemin sans chaîne de requête ; ni corps, ni en-têtes, ni jetons ne sont journalisés.
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} => {StatusCode} en {Elapsed:0.0} ms";
            options.EnrichDiagnosticContext = (diagnostics, http) =>
            {
                var userId = http.User.FindFirst("sub")?.Value;
                if (userId is not null)
                {
                    diagnostics.Set("UserId", userId);
                }
            };
        });

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        app.UseCors();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/api/v1/health", new HealthCheckOptions { ResponseWriter = WriteHealthAsync })
            .AllowAnonymous()
            .DisableRateLimiting();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference(options => options.WithTitle("CreditFlow API")).AllowAnonymous();
        }

        return app;
    }

    private static Task WriteHealthAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString()),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
