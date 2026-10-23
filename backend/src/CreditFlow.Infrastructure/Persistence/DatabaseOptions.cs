namespace CreditFlow.Infrastructure.Persistence;

public enum DatabaseProvider
{
    SqlServer,
    PostgreSQL,
}

/// <summary>Section <c>Database</c> ; la chaîne de connexion est lue dans <c>ConnectionStrings:Default</c>.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "Default";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;

    /// <summary>Applique les migrations au démarrage (docker compose, tests d'intégration).</summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>Tentatives de connexion au démarrage (la base peut démarrer après l'API).</summary>
    public int StartupConnectRetries { get; set; } = 10;
}
