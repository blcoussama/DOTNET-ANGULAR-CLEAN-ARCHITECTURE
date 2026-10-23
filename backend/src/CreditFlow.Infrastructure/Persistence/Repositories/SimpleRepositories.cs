using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Common;
using CreditFlow.Application.Dashboard;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Infrastructure.Persistence.Repositories;

internal sealed class ClientProfileRepository(CreditFlowDbContext db) : IClientProfileRepository
{
    public Task<ClientProfile?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        db.ClientProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public void Add(ClientProfile profile) => db.ClientProfiles.Add(profile);
}

internal sealed class CreditProductRepository(CreditFlowDbContext db) : ICreditProductRepository
{
    public Task<CreditProduct?> GetAsync(ProductType type, CancellationToken cancellationToken) =>
        db.CreditProducts.FirstOrDefaultAsync(p => p.Type == type, cancellationToken);

    public async Task<IReadOnlyList<CreditProduct>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = db.CreditProducts.AsQueryable();
        if (activeOnly)
        {
            query = query.Where(p => p.IsActive);
        }

        var products = await query.ToListAsync(cancellationToken);
        return [.. products.OrderBy(p => p.Type)];
    }
}

internal sealed class UnitOfWork(CreditFlowDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw Conflict(ex);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Deux écritures simultanées sur le même agrégat (ex. même rang d'événement) : même traitement qu'un conflit de version.
            throw Conflict(ex);
        }
    }

    public void DiscardChanges() => db.ChangeTracker.Clear();

    internal static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException switch
    {
        Npgsql.PostgresException postgres => postgres.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation,
        Microsoft.Data.SqlClient.SqlException sql => sql.Number is 2601 or 2627,
        _ => false,
    };

    private static ConflictException Conflict(Exception inner) =>
        new("La ressource a été modifiée par une autre opération. Rechargez-la avant de réessayer.", "concurrency_conflict", inner);
}

internal sealed class UserDirectory(CreditFlowDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, UserSummary>> GetAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, UserSummary>();
        }

        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UserSummary(u.Id, u.Email ?? string.Empty, u.FirstName, u.LastName))
            .ToDictionaryAsync(u => u.Id, cancellationToken);
    }
}

internal sealed class DashboardDataSource(CreditFlowDbContext db) : IDashboardDataSource
{
    public async Task<IReadOnlyDictionary<ApplicationStatus, int>> CountByStatusAsync(CancellationToken cancellationToken) =>
        await db.CreditApplications.AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<SubmittedApplicationSnapshot>> GetSubmittedAsync(CancellationToken cancellationToken) =>
        await db.CreditApplications.AsNoTracking()
            .Where(a => a.SubmittedAt != null)
            .Select(a => new SubmittedApplicationSnapshot(a.Status, a.ProductType, a.Amount, a.RiskGrade, a.SubmittedAt!.Value, a.DecidedAt))
            .ToListAsync(cancellationToken);
}
