using CreditFlow.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreditFlow.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applique les migrations si <c>Database:MigrateOnStartup</c>, puis sème les données de référence
    /// (rôles, produits) et, en Development uniquement, les données de démonstration.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, bool isDevelopment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("CreditFlow.Database");
        var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var db = provider.GetRequiredService<CreditFlowDbContext>();

        if (options.MigrateOnStartup)
        {
            await MigrateWithRetryAsync(db, options.StartupConnectRetries, logger, cancellationToken);
        }
        else
        {
            try
            {
                var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pending.Count > 0)
                {
                    logger.LogWarning(
                        "{Count} migration(s) en attente : lancez « dotnet ef database update » ou Database:MigrateOnStartup=true. Initialisation des données ignorée.",
                        pending.Count);
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Base de données injoignable au démarrage : initialisation des données ignorée");
                return;
            }
        }

        await ReferenceDataSeeder.SeedAsync(provider, logger, cancellationToken);

        var seed = provider.GetRequiredService<IOptions<SeedOptions>>().Value;
        if (isDevelopment && seed.DemoData)
        {
            await DemoDataSeeder.SeedAsync(provider, logger, cancellationToken);
        }
    }

    private static async Task MigrateWithRetryAsync(CreditFlowDbContext db, int retries, ILogger logger, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Migrations appliquées ({Provider})", db.Database.ProviderName);
                return;
            }
            catch (Exception ex) when (attempt < Math.Max(1, retries) && ex is not OperationCanceledException && IsTransient(ex))
            {
                logger.LogWarning("Base indisponible (tentative {Attempt}/{Max}), nouvel essai dans 3 s", attempt, retries);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is System.Net.Sockets.SocketException or TimeoutException
        || ex.GetType().Name is "NpgsqlException" or "SqlException"
        || (ex.InnerException is not null && IsTransient(ex.InnerException));
}
