using CreditFlow.Application.Applications;
using CreditFlow.Application.Common;
using CreditFlow.Application.Review;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Products;

namespace CreditFlow.Application.Abstractions;

public interface ICreditApplicationRepository
{
    /// <summary>Charge l'agrégat complet (pièces et événements) pour modification.</summary>
    Task<CreditApplication?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(CreditApplication application);

    Task<PagedResult<ApplicationSummaryDto>> ListForClientAsync(
        Guid clientId,
        ApplicationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResult<ReviewQueueItemDto>> ListForReviewAsync(ReviewQueueFilter filter, CancellationToken cancellationToken);
}

public interface IClientProfileRepository
{
    Task<ClientProfile?> GetAsync(Guid userId, CancellationToken cancellationToken);

    void Add(ClientProfile profile);
}

public interface ICreditProductRepository
{
    Task<CreditProduct?> GetAsync(ProductType type, CancellationToken cancellationToken);

    Task<IReadOnlyList<CreditProduct>> ListAsync(bool activeOnly, CancellationToken cancellationToken);
}

/// <summary>Valide la transaction ; un conflit de concurrence est traduit en <see cref="ConflictException"/>.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Abandonne les modifications suivies, avant de recharger un agrégat et de rejouer une opération après un conflit.</summary>
    void DiscardChanges();
}

/// <summary>Génère des références lisibles uniques <c>CF-AAAA-NNNNNN</c>.</summary>
public interface IReferenceGenerator
{
    Task<string> NextAsync(int year, CancellationToken cancellationToken);
}

/// <summary>Identité minimale d'un utilisateur, pour l'affichage (historique, assignation).</summary>
public sealed record UserSummary(Guid Id, string Email, string FirstName, string LastName)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, UserSummary>> GetAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
}
