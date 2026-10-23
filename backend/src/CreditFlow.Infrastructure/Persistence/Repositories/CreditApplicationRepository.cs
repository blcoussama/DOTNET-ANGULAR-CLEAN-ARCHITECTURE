using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Applications;
using CreditFlow.Application.Common;
using CreditFlow.Application.Review;
using CreditFlow.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Infrastructure.Persistence.Repositories;

internal sealed class CreditApplicationRepository(CreditFlowDbContext db) : ICreditApplicationRepository
{
    public Task<CreditApplication?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.CreditApplications
            .Include(a => a.Documents)
            .Include(a => a.Events)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public void Add(CreditApplication application) => db.CreditApplications.Add(application);

    public async Task<PagedResult<ApplicationSummaryDto>> ListForClientAsync(
        Guid clientId,
        ApplicationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.CreditApplications.AsNoTracking().Where(a => a.ClientId == clientId);
        if (status is { } s)
        {
            query = query.Where(a => a.Status == s);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ApplicationSummaryDto(
                a.Id,
                a.Reference,
                a.ProductType,
                a.Amount,
                a.DurationMonths,
                a.Status,
                a.MonthlyPayment,
                a.CreatedAt,
                a.UpdatedAt,
                a.SubmittedAt,
                a.DecidedAt))
            .ToListAsync(cancellationToken);
        return new PagedResult<ApplicationSummaryDto>(items, page, pageSize, total);
    }

    public async Task<PagedResult<ReviewQueueItemDto>> ListForReviewAsync(ReviewQueueFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query =
            from a in db.CreditApplications.AsNoTracking()
            join c in db.Users.AsNoTracking() on a.ClientId equals c.Id
            join an in db.Users.AsNoTracking() on a.AssignedAnalystId equals an.Id into analysts
            from an in analysts.DefaultIfEmpty()
            where a.SubmittedAt != null
            select new { Application = a, Client = c, Analyst = an };

        if (filter.Status is { } status)
        {
            query = query.Where(x => x.Application.Status == status);
        }

        if (filter.RiskGrade is { } grade)
        {
            query = query.Where(x => x.Application.RiskGrade == grade);
        }

        if (filter.ProductType is { } product)
        {
            query = query.Where(x => x.Application.ProductType == product);
        }

        if (filter.AssignedTo is { } assignee)
        {
            query = query.Where(x => x.Application.AssignedAnalystId == assignee);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
#pragma warning disable CA1304, CA1311, CA1862 // ToLower() est traduit en SQL (LOWER), portable PostgreSQL / SQL Server.
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Application.Reference.ToLower().Contains(term)
                || x.Client.FirstName.ToLower().Contains(term)
                || x.Client.LastName.ToLower().Contains(term)
                || (x.Client.FirstName + " " + x.Client.LastName).ToLower().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var ascending = filter.SortDir == SortDirection.Asc;
        var ordered = filter.SortBy switch
        {
            ReviewSortField.Amount => ascending
                ? query.OrderBy(x => x.Application.Amount)
                : query.OrderByDescending(x => x.Application.Amount),
            ReviewSortField.RiskScore => ascending
                ? query.OrderBy(x => x.Application.RiskScore)
                : query.OrderByDescending(x => x.Application.RiskScore),
            _ => ascending
                ? query.OrderBy(x => x.Application.SubmittedAt)
                : query.OrderByDescending(x => x.Application.SubmittedAt),
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await ordered
            .ThenBy(x => x.Application.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => new ReviewQueueItemDto(
                x.Application.Id,
                x.Application.Reference,
                x.Application.ClientId,
                x.Client.FirstName + " " + x.Client.LastName,
                x.Application.ProductType,
                x.Application.Amount,
                x.Application.DurationMonths,
                x.Application.Status,
                x.Application.MonthlyPayment,
                x.Application.DebtRatio,
                x.Application.RiskScore,
                x.Application.RiskGrade,
                x.Application.Recommendation,
                x.Application.AssignedAnalystId,
                x.Analyst == null ? null : x.Analyst.FirstName + " " + x.Analyst.LastName,
                x.Application.SubmittedAt,
                x.Application.UpdatedAt))
            .ToListAsync(cancellationToken);
        return new PagedResult<ReviewQueueItemDto>(items, filter.Page, filter.PageSize, total);
    }
}
