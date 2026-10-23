using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CreditFlow.Infrastructure.Persistence;

/// <summary>
/// Fabriques utilisées uniquement par <c>dotnet ef</c> pour générer les migrations (aucune connexion n'est ouverte).
/// La chaîne peut être surchargée par la variable <c>CREDITFLOW_DESIGN_CONNECTION</c> pour <c>database update</c>.
/// </summary>
internal sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresCreditFlowDbContext>
{
    public PostgresCreditFlowDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CREDITFLOW_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=creditflow_design;Username=design";
        var options = new DbContextOptionsBuilder<PostgresCreditFlowDbContext>()
            .UseNpgsql(connection)
            .Options;
        return new PostgresCreditFlowDbContext(options);
    }
}

internal sealed class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerCreditFlowDbContext>
{
    public SqlServerCreditFlowDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CREDITFLOW_DESIGN_CONNECTION")
            ?? "Server=localhost;Database=creditflow_design;Integrated Security=true;TrustServerCertificate=true";
        var options = new DbContextOptionsBuilder<SqlServerCreditFlowDbContext>()
            .UseSqlServer(connection)
            .Options;
        return new SqlServerCreditFlowDbContext(options);
    }
}
