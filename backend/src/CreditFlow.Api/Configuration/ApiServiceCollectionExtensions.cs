using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CreditFlow.Api.Errors;
using CreditFlow.Api.Http;
using CreditFlow.Api.OpenApi;
using CreditFlow.Application.Common;
using CreditFlow.Domain.Common;
using CreditFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CreditFlow.Api.Configuration;

public static class Policies
{
    public const string Client = "ClientOnly";
    public const string Reviewer = "Reviewer";
    public const string Admin = "AdminOnly";
    public const string AuthRateLimit = "auth";
}

/// <summary>Section <c>RateLimiting</c> (fenêtres fixes, par utilisateur ou adresse IP).</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitLimit { get; set; } = 10;

    public int AuthWindowSeconds { get; set; } = 60;

    public int GlobalPermitLimit { get; set; } = 300;

    public int GlobalWindowSeconds { get; set; } = 60;
}

internal static class ApiServiceCollectionExtensions
{
    public const long MaxRequestBodyBytes = 6L * 1024 * 1024;

    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(EnumWire.NamingPolicy, allowIntegerValues: false));
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            })
            .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
            {
                var problem = InvalidModelState(context);
                Customize(problem, context.HttpContext);
                return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
            });

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            Customize(context.ProblemDetails, context.HttpContext));
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddSingleton<RefreshTokenCookie>();
        services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxRequestBodyBytes);
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        services.AddJwtAuthentication();
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Client, p => p.RequireAuthenticatedUser().RequireRole(Roles.Client))
            .AddPolicy(Policies.Reviewer, p => p.RequireAuthenticatedUser().RequireRole(Roles.Analyst, Roles.Admin))
            .AddPolicy(Policies.Admin, p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin))
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddCreditFlowRateLimiting();
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            if (origins.Length > 0)
            {
                options.AddDefaultPolicy(policy => policy
                    .WithOrigins(origins)
                    .AllowCredentials()
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                    .WithHeaders("Content-Type", "Authorization", "If-Match", "X-Requested-With")
                    .WithExposedHeaders("Content-Disposition", "Location", "ETag", "Retry-After"));
            }
        });

        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
        return services;
    }

    private static void AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(jwt.SigningKeyBytes()),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = CreditFlowClaims.Subject,
                    RoleClaimType = CreditFlowClaims.Role,
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirst(CreditFlowClaims.Subject)?.Value;
                        var stamp = context.Principal?.FindFirst(CreditFlowClaims.SecurityStamp)?.Value;
                        var validator = context.HttpContext.RequestServices.GetRequiredService<UserSessionValidator>();
                        if (!Guid.TryParse(subject, out var userId) || stamp is null
                            || !await validator.IsValidAsync(userId, stamp, context.HttpContext.RequestAborted))
                        {
                            context.Fail("Jeton révoqué ou compte désactivé.");
                        }
                    },
                };
            });
    }

    private static void AddCreditFlowRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .Validate(o => o.AuthPermitLimit > 0 && o.GlobalPermitLimit > 0 && o.AuthWindowSeconds > 0 && o.GlobalWindowSeconds > 0, "RateLimiting : valeurs strictement positives attendues.");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                var problems = http.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Detail = "Trop de requêtes. Patientez quelques instants avant de réessayer.",
                    },
                });
            };

            options.AddPolicy(Policies.AuthRateLimit, http =>
            {
                var limits = http.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    "auth:" + ClientAddress(http),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(limits.AuthWindowSeconds),
                        QueueLimit = 0,
                    });
            });

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var limits = http.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var subject = http.User.FindFirst(CreditFlowClaims.Subject)?.Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    subject is null ? "ip:" + ClientAddress(http) : "user:" + subject,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.GlobalPermitLimit,
                        Window = TimeSpan.FromSeconds(limits.GlobalWindowSeconds),
                        QueueLimit = 0,
                    });
            });
        });
    }

    /// <summary>Titre français, statut, instance et identifiant de trace sur chaque ProblemDetails.</summary>
    private static void Customize(ProblemDetails problem, HttpContext http)
    {
        var status = problem.Status ?? http.Response.StatusCode;
        problem.Status = status;
        problem.Title = status == StatusCodes.Status400BadRequest && problem is ValidationProblemDetails
            ? "Données invalides"
            : ProblemTitles.For(status);
        problem.Instance ??= http.Request.Path;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;
    }

    private static string ClientAddress(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>Erreurs de liaison de modèle (JSON mal formé, type incorrect) au format ValidationProblemDetails.</summary>
    private static ValidationProblemDetails InvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .GroupBy(entry => ValidationErrorKeys.Normalize(entry.Key))
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(entry => entry.Value!.Errors)
                    .Select(error => error.Exception is not null || error.ErrorMessage.StartsWith("The JSON value", StringComparison.Ordinal) || error.ErrorMessage.Contains("field is required", StringComparison.Ordinal)
                        ? "Valeur absente ou invalide pour ce champ."
                        : error.ErrorMessage)
                    .Distinct()
                    .ToArray());

        if (errors.Count > 1)
        {
            errors.Remove("request");
        }

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Detail = "Le corps ou les paramètres de la requête sont invalides.",
        };
    }
}
