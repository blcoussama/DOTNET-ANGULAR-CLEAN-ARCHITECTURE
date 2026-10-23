using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;
using CreditFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Infrastructure.Persistence;

/// <summary>
/// Contexte EF Core commun. Deux sous-classes (PostgreSQL, SQL Server) portent chacune leur jeu de migrations.
/// </summary>
public abstract class CreditFlowDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    protected CreditFlowDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();

    public DbSet<CreditProduct> CreditProducts => Set<CreditProduct>();

    public DbSet<CreditApplication> CreditApplications => Set<CreditApplication>();

    public DbSet<ApplicationDocument> ApplicationDocuments => Set<ApplicationDocument>();

    public DbSet<ApplicationEvent> ApplicationEvents => Set<ApplicationEvent>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<ReferenceCounter> ReferenceCounters => Set<ReferenceCounter>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CreditFlowDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        base.ConfigureConventions(configurationBuilder);

        // Montants en MAD : precision 18, 2 décimales (évite la troncature silencieuse de SQL Server).
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        // Énumérations stockées en clair (lisibles en base et stables si l'ordre des valeurs change).
        configurationBuilder.Properties<ApplicationStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<DocumentType>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<ProductType>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<EmploymentType>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<RiskGrade>().HaveConversion<string>().HaveMaxLength(1);
        configurationBuilder.Properties<Recommendation>().HaveConversion<string>().HaveMaxLength(32);
    }
}

public sealed class PostgresCreditFlowDbContext(DbContextOptions<PostgresCreditFlowDbContext> options)
    : CreditFlowDbContext(options);

public sealed class SqlServerCreditFlowDbContext(DbContextOptions<SqlServerCreditFlowDbContext> options)
    : CreditFlowDbContext(options);
