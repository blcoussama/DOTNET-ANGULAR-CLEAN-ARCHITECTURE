using System.Data.Common;
using CreditFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base jetable des tests : CREDITFLOW_TEST_DB (chaîne de connexion) et CREDITFLOW_TEST_DB_PROVIDER (PostgreSQL | SqlServer).
/// Le schéma est supprimé puis recréé par les migrations ; refus si le nom de la base ne contient pas « test ».
/// </summary>
public static class TestDatabase
{
    public const string DefaultConnection = "Host=localhost;Port=5432;Database=creditflow_api_test;Username=postgres;Password=postgres";

    public static string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("CREDITFLOW_TEST_DB") is { Length: > 0 } value ? value : DefaultConnection;

    public static string Provider { get; } =
        Environment.GetEnvironmentVariable("CREDITFLOW_TEST_DB_PROVIDER") is { Length: > 0 } value ? value : "PostgreSQL";

    public static void EnsureDisposableDatabase()
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = ConnectionString };
        var name = builder.TryGetValue("Database", out var database) ? database?.ToString()
            : builder.TryGetValue("Initial Catalog", out var catalog) ? catalog?.ToString()
            : null;
        if (string.IsNullOrWhiteSpace(name) || !name.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refus d'exécuter les tests d'intégration : la base « {name} » ne contient pas « test » dans son nom (CREDITFLOW_TEST_DB).");
        }
    }

    /// <summary>Supprime la base de test ; les migrations la recréent au démarrage de l'API (Database:MigrateOnStartup).</summary>
    public static async Task DropAsync()
    {
        EnsureDisposableDatabase();
        await using CreditFlowDbContext context = Provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            ? new SqlServerCreditFlowDbContext(new DbContextOptionsBuilder<SqlServerCreditFlowDbContext>().UseSqlServer(ConnectionString).Options)
            : new PostgresCreditFlowDbContext(new DbContextOptionsBuilder<PostgresCreditFlowDbContext>().UseNpgsql(ConnectionString).Options);
        await context.Database.EnsureDeletedAsync();
    }
}
