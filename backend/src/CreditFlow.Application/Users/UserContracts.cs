using CreditFlow.Application.Common;

namespace CreditFlow.Application.Users;

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LockoutEnd);

public sealed record UserListQuery(int Page = 1, int PageSize = 20, string? Role = null, string? Search = null);

public sealed record CreateUserRequest(string? Email, string? FirstName, string? LastName, string? Role, string? TemporaryPassword);

public sealed record UpdateUserRequest(string? Role, bool? IsActive);

public interface IUserAdministrationService
{
    Task<PagedResult<AdminUserDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken);

    Task<AdminUserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    /// <summary>Change le rôle et/ou l'état ; un administrateur ne peut ni se désactiver ni se retirer son rôle.</summary>
    Task<AdminUserDto> UpdateAsync(CurrentActor actor, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);
}
