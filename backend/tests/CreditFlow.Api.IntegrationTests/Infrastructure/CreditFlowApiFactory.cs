using System.Security.Cryptography;
using CreditFlow.Domain.Common;
using CreditFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CreditFlow.Api.IntegrationTests.Infrastructure;

/// <summary>
/// API réelle (pipeline complet) sur la base jetable, avec clé JWT et dossier de stockage aléatoires.
/// </summary>
public class CreditFlowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Motdepasse1!";

    private readonly Dictionary<string, string?> _overrides;

    public CreditFlowApiFactory()
        : this([])
    {
    }

    protected CreditFlowApiFactory(Dictionary<string, string?> overrides)
    {
        _overrides = overrides;
    }

    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "creditflow-tests-" + Guid.NewGuid().ToString("N"));

    public string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public virtual async Task InitializeAsync()
    {
        await TestDatabase.DropAsync();
        _ = Server; // démarre l'hôte : migrations + données de référence
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
    }

    /// <summary>Client HTTP sans gestion automatique des cookies (on contrôle le refresh token explicitement).</summary>
    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false, AllowAutoRedirect = false });

    /// <summary>Crée directement un compte (Analyst / Admin) en base, comme le ferait le seed.</summary>
    public async Task<(Guid Id, string Email)> CreateUserAsync(string role, string? emailPrefix = null)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{emailPrefix ?? role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = role,
            LastName = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        (await users.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();
        (await users.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
        return (user.Id, email);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        var settings = new Dictionary<string, string?>
        {
            ["Database:Provider"] = TestDatabase.Provider,
            ["Database:MigrateOnStartup"] = "true",
            ["ConnectionStrings:Default"] = TestDatabase.ConnectionString,
            ["Jwt:SigningKey"] = SigningKey,
            ["Storage:RootPath"] = StorageRoot,
            ["RateLimiting:AuthPermitLimit"] = "100000",
            ["RateLimiting:GlobalPermitLimit"] = "100000",
            ["Seed:DemoData"] = "false",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        };
        foreach (var (key, value) in _overrides)
        {
            settings[key] = value;
        }

        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SharedApiDefinition : ICollectionFixture<CreditFlowApiFactory>
{
    public const string Name = "api";
}

public static class RoleNames
{
    public const string Client = Roles.Client;
    public const string Analyst = Roles.Analyst;
    public const string Admin = Roles.Admin;
}
