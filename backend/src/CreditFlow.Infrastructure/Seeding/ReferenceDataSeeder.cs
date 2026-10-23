using CreditFlow.Domain.Common;
using CreditFlow.Domain.Products;
using CreditFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CreditFlow.Infrastructure.Seeding;

/// <summary>Données de référence semées dans tous les environnements : rôles et produits initiaux (idempotent).</summary>
internal static class ReferenceDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() });
                if (!result.Succeeded)
                {
                    logger.LogWarning("Rôle {Role} non créé : {Errors}", role, string.Join(", ", result.Errors.Select(e => e.Code)));
                }
            }
        }

        var db = services.GetRequiredService<CreditFlowDbContext>();
        var clock = services.GetRequiredService<TimeProvider>();
        var existing = await db.CreditProducts.Select(p => p.Type).ToListAsync(cancellationToken);
        var missing = CreditProduct.Defaults(clock.GetUtcNow()).Where(p => !existing.Contains(p.Type)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        db.CreditProducts.AddRange(missing);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("{Count} produit(s) de crédit initialisé(s)", missing.Count);
        }
        catch (DbUpdateException)
        {
            // Une autre instance a semé les produits au même moment.
            db.ChangeTracker.Clear();
        }
    }
}
