using CreditFlow.Application.Common;
using CreditFlow.Application.Users;
using CreditFlow.Domain.Common;
using CreditFlow.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CreditFlow.Infrastructure.Identity;

/// <summary>Gestion des comptes par l'administrateur.</summary>
internal sealed class UserAdministrationService(
    UserManager<ApplicationUser> userManager,
    CreditFlowDbContext db,
    IValidator<UserListQuery> listValidator,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator,
    TimeProvider clock,
    ILogger<UserAdministrationService> logger) : IUserAdministrationService
{
    public async Task<PagedResult<AdminUserDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await listValidator.EnsureValidAsync(query, cancellationToken);

        var users =
            from u in db.Users.AsNoTracking()
            let role = (from ur in db.UserRoles
                        join r in db.Roles on ur.RoleId equals r.Id
                        where ur.UserId == u.Id
                        select r.Name).FirstOrDefault()
            select new { User = u, Role = role };

        if (query.Role is { } roleFilter)
        {
            users = users.Where(x => x.Role == roleFilter);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
#pragma warning disable CA1304, CA1311, CA1862 // ToLower() est traduit en SQL (LOWER), portable PostgreSQL / SQL Server.
            var term = query.Search.Trim().ToLowerInvariant();
            users = users.Where(x =>
                x.User.Email!.ToLower().Contains(term)
                || x.User.FirstName.ToLower().Contains(term)
                || x.User.LastName.ToLower().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var total = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderByDescending(x => x.User.CreatedAt)
            .ThenBy(x => x.User.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new AdminUserDto(
                x.User.Id,
                x.User.Email ?? string.Empty,
                x.User.FirstName,
                x.User.LastName,
                x.Role ?? Roles.Client,
                x.User.IsActive,
                x.User.CreatedAt,
                x.User.LockoutEnd))
            .ToListAsync(cancellationToken);
        return new PagedResult<AdminUserDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<AdminUserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await createValidator.EnsureValidAsync(request, cancellationToken);

        var email = request.Email!.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("Un compte existe déjà avec cette adresse e-mail.", "email_taken");
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = request.FirstName!.Trim(),
            LastName = request.LastName!.Trim(),
            IsActive = true,
            CreatedAt = clock.GetUtcNow(),
        };

        await IdentityResults.EnsureSucceededAsync(() => userManager.CreateAsync(user, request.TemporaryPassword!), "temporaryPassword");
        await IdentityResults.EnsureSucceededAsync(() => userManager.AddToRoleAsync(user, request.Role!), "role");
        logger.LogInformation("Compte {Role} {UserId} créé par un administrateur", request.Role, user.Id);
        return ToDto(user, request.Role!);
    }

    public async Task<AdminUserDto> UpdateAsync(CurrentActor actor, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        await updateValidator.EnsureValidAsync(request, cancellationToken);

        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Utilisateur introuvable.");
        var currentRole = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? Roles.Client;

        if (userId == actor.UserId)
        {
            if (request.IsActive == false)
            {
                throw ValidationExtensions.ValidationError("isActive", "Vous ne pouvez pas désactiver votre propre compte.");
            }

            if (request.Role is { } ownRole && ownRole != Roles.Admin)
            {
                throw ValidationExtensions.ValidationError("role", "Vous ne pouvez pas retirer votre propre rôle administrateur.");
            }
        }

        if (request.Role is not null && currentRole == Roles.Client)
        {
            throw ValidationExtensions.ValidationError("role", "Le rôle d'un compte client ne peut pas être modifié (seuls Analyst ↔ Admin).");
        }

        var revokeReason = default(string);
        if (request.Role is { } newRole && newRole != currentRole)
        {
            await IdentityResults.EnsureSucceededAsync(() => userManager.RemoveFromRolesAsync(user, [currentRole]), "role");
            await IdentityResults.EnsureSucceededAsync(() => userManager.AddToRoleAsync(user, newRole), "role");
            currentRole = newRole;
            revokeReason = RevocationReasons.RoleChanged;
        }

        if (request.IsActive is { } isActive && isActive != user.IsActive)
        {
            user.IsActive = isActive;
            await IdentityResults.EnsureSucceededAsync(() => userManager.UpdateAsync(user), "isActive");
            if (!isActive)
            {
                revokeReason = RevocationReasons.AccountDisabled;
            }
        }

        if (revokeReason is not null)
        {
            // Le changement de security stamp invalide immédiatement les jetons d'accès en circulation.
            await IdentityResults.EnsureSucceededAsync(() => userManager.UpdateSecurityStampAsync(user), "request");
            var now = clock.GetUtcNow();
            await db.RefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.RevocationReason, revokeReason), cancellationToken);
            logger.LogInformation("Jetons du compte {UserId} révoqués ({Reason})", user.Id, revokeReason);
        }

        return ToDto(user, currentRole);
    }

    private static AdminUserDto ToDto(ApplicationUser user, string role) =>
        new(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, role, user.IsActive, user.CreatedAt, user.LockoutEnd);
}
