using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Auth;
using CreditFlow.Application.Dashboard;
using CreditFlow.Application.Users;
using CreditFlow.Infrastructure.Identity;
using CreditFlow.Infrastructure.Persistence;
using CreditFlow.Infrastructure.Persistence.Repositories;
using CreditFlow.Infrastructure.Seeding;
using CreditFlow.Infrastructure.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CreditFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        // Clés Data Protection (jetons Identity) : persistées si un dossier est configuré (volume Docker),
        // sinon conservées par défaut par ASP.NET Core (profil utilisateur ou mémoire).
        var dataProtection = services.AddDataProtection().SetApplicationName("CreditFlow");
        if (configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .Validate(o => Enum.IsDefined(o.Provider), "Database:Provider doit valoir SqlServer ou PostgreSQL.")
            .ValidateOnStart();
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.RootPath), "Storage:RootPath est obligatoire.")
            .ValidateOnStart();
        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);

        // Les deux contextes sont enregistrés ; seul celui du fournisseur configuré est résolu (lecture paresseuse).
        services.AddDbContext<PostgresCreditFlowDbContext>((sp, options) =>
            options.UseNpgsql(ConnectionString(sp), npgsql => npgsql.CommandTimeout(30)));
        services.AddDbContext<SqlServerCreditFlowDbContext>((sp, options) =>
            options.UseSqlServer(ConnectionString(sp), sql => sql.CommandTimeout(30)));
        services.AddScoped<CreditFlowDbContext>(sp =>
            sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.Provider == DatabaseProvider.PostgreSQL
                ? sp.GetRequiredService<PostgresCreditFlowDbContext>()
                : sp.GetRequiredService<SqlServerCreditFlowDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = PasswordPolicy.MinLength;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 1;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<CreditFlowDbContext>();

        services.AddScoped<ICreditApplicationRepository, CreditApplicationRepository>();
        services.AddScoped<IClientProfileRepository, ClientProfileRepository>();
        services.AddScoped<ICreditProductRepository, CreditProductRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IDashboardDataSource, DashboardDataSource>();
        services.AddSingleton<IReferenceGenerator, ReferenceGenerator>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddSingleton<TokenService>();
        services.AddScoped<UserSessionValidator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();

        services.AddHealthChecks().AddDbContextCheck<CreditFlowDbContext>("database", tags: ["ready"]);
        return services;
    }

    private static string ConnectionString(IServiceProvider services) =>
        services.GetRequiredService<IConfiguration>().GetConnectionString(DatabaseOptions.ConnectionStringName) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException("ConnectionStrings:Default est obligatoire.");
}
