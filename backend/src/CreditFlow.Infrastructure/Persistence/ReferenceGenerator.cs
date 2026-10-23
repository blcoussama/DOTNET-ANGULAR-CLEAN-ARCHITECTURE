using System.Globalization;
using CreditFlow.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CreditFlow.Infrastructure.Persistence;

/// <summary>
/// Références séquentielles par année, incrémentées atomiquement (UPDATE … SET LastValue = LastValue + 1)
/// dans une transaction dédiée, indépendante de l'unité de travail appelante. Un trou dans la séquence est
/// possible si l'enregistrement de la demande échoue ensuite (comportement classique d'une séquence).
/// </summary>
internal sealed class ReferenceGenerator(IServiceScopeFactory scopeFactory) : IReferenceGenerator
{
    private const int MaxAttempts = 5;
    private const int MaxValue = 999_999;

    public async Task<string> NextAsync(int year, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CreditFlowDbContext>();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var updated = await db.ReferenceCounters
                .Where(c => c.Year == year)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastValue, c => c.LastValue + 1), cancellationToken);

            if (updated == 0)
            {
                try
                {
                    db.ReferenceCounters.Add(new ReferenceCounter { Year = year, LastValue = 1 });
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException) when (attempt < MaxAttempts)
                {
                    // Création concurrente du compteur de l'année : on recommence avec la ligne existante.
                    await transaction.RollbackAsync(cancellationToken);
                    db.ChangeTracker.Clear();
                    continue;
                }
            }

            var value = await db.ReferenceCounters
                .Where(c => c.Year == year)
                .Select(c => c.LastValue)
                .SingleAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (value > MaxValue)
            {
                throw new InvalidOperationException($"Capacité annuelle de références épuisée pour {year}.");
            }

            return string.Create(CultureInfo.InvariantCulture, $"CF-{year:D4}-{value:D6}");
        }

        throw new InvalidOperationException("Impossible de générer une référence de demande.");
    }
}
