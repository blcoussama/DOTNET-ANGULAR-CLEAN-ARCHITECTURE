using CreditFlow.Application.Auth;
using CreditFlow.Application.Common;
using CreditFlow.Domain.Common;
using CreditFlow.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreditFlow.Infrastructure.Identity;

/// <summary>Inscription, connexion, rotation des refresh tokens (avec détection de rejeu) et déconnexion.</summary>
internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    CreditFlowDbContext db,
    TokenService tokens,
    IOptions<JwtOptions> jwtOptions,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await registerValidator.EnsureValidAsync(request, cancellationToken);

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
            FirstName = request.FirstName!.Trim(),
            LastName = request.LastName!.Trim(),
            IsActive = true,
            CreatedAt = clock.GetUtcNow(),
        };

        await IdentityResults.EnsureSucceededAsync(() => userManager.CreateAsync(user, request.Password!), "password");
        await IdentityResults.EnsureSucceededAsync(() => userManager.AddToRoleAsync(user, Roles.Client), "role");
        logger.LogInformation("Nouveau compte client {UserId}", user.Id);
        return ToDto(user, Roles.Client);
    }

    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await loginValidator.EnsureValidAsync(request, cancellationToken);

        var user = await userManager.FindByEmailAsync(request.Email!.Trim());
        if (user is null)
        {
            throw InvalidCredentials();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw Locked();
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password!))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Échec de connexion pour le compte {UserId}", user.Id);
            if (await userManager.IsLockedOutAsync(user))
            {
                throw Locked();
            }

            throw InvalidCredentials();
        }

        if (!user.IsActive)
        {
            throw new AuthenticationFailedException("Ce compte est désactivé. Contactez votre conseiller.", "account_disabled");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var role = await GetRoleAsync(user);
        var session = Issue(user, role, familyId: Guid.CreateVersion7());
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<AuthSession> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new AuthenticationFailedException("Session absente ou expirée. Reconnectez-vous.", "refresh_missing");
        }

        var now = clock.GetUtcNow();
        var hash = TokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw new AuthenticationFailedException("Session invalide. Reconnectez-vous.", "refresh_invalid");

        if (stored.RevokedAt is not null && stored.RevocationReason != RevocationReasons.Rotated)
        {
            // Session déjà fermée (déconnexion, compte désactivé, rôle modifié, famille révoquée).
            throw new AuthenticationFailedException("Session expirée ou révoquée. Reconnectez-vous.", "refresh_revoked");
        }

        if (stored.RevokedAt is not null)
        {
            // Rejeu d'un jeton déjà consommé : vol probable, toute la famille est révoquée.
            logger.LogWarning("Rejeu de refresh token détecté pour l'utilisateur {UserId} (famille {FamilyId})", stored.UserId, stored.FamilyId);
            await RevokeFamilyAsync(stored.FamilyId, RevocationReasons.Reused, cancellationToken);
            throw new AuthenticationFailedException("Session révoquée pour raison de sécurité. Reconnectez-vous.", "refresh_reused");
        }

        if (stored.ExpiresAt <= now)
        {
            throw new AuthenticationFailedException("Session expirée. Reconnectez-vous.", "refresh_expired");
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            await RevokeFamilyAsync(stored.FamilyId, RevocationReasons.AccountDisabled, cancellationToken);
            throw new AuthenticationFailedException("Ce compte est désactivé. Contactez votre conseiller.", "account_disabled");
        }

        var role = await GetRoleAsync(user);
        var session = Issue(user, role, stored.FamilyId, rotated: stored);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Deux rotations simultanées du même jeton : la seconde est traitée comme un rejeu.
            db.ChangeTracker.Clear();
            await RevokeFamilyAsync(stored.FamilyId, RevocationReasons.Reused, cancellationToken);
            throw new AuthenticationFailedException("Session révoquée pour raison de sécurité. Reconnectez-vous.", "refresh_reused");
        }

        return session;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = TokenService.HashRefreshToken(refreshToken);
        var familyId = await db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .FirstOrDefaultAsync(cancellationToken);
        if (familyId is { } family)
        {
            await RevokeFamilyAsync(family, RevocationReasons.Logout, cancellationToken);
        }
    }

    public async Task<UserDto> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            throw new NotFoundException("Utilisateur introuvable.");
        }

        return ToDto(user, await GetRoleAsync(user));
    }

    internal static UserDto ToDto(ApplicationUser user, string role) =>
        new(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, role);

    private async Task<string> GetRoleAsync(ApplicationUser user) =>
        (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? Roles.Client;

    private AuthSession Issue(ApplicationUser user, string role, Guid familyId, RefreshToken? rotated = null)
    {
        var now = clock.GetUtcNow();
        var plain = TokenService.GenerateRefreshToken();
        var entity = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = TokenService.HashRefreshToken(plain),
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays),
        };
        db.RefreshTokens.Add(entity);

        if (rotated is not null)
        {
            rotated.RevokedAt = now;
            rotated.RevocationReason = RevocationReasons.Rotated;
            rotated.ReplacedByTokenId = entity.Id;
        }

        var response = new AuthResponse(tokens.CreateAccessToken(user, role), tokens.AccessTokenLifetimeSeconds, ToDto(user, role));
        return new AuthSession(response, plain, entity.ExpiresAt);
    }

    private async Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.RevocationReason, reason),
                cancellationToken);
    }

    private static AuthenticationFailedException InvalidCredentials() =>
        new("Adresse e-mail ou mot de passe incorrect.", "invalid_credentials");

    private static AccountLockedException Locked() =>
        new("Compte temporairement verrouillé après plusieurs tentatives infructueuses. Réessayez dans 15 minutes.");
}

/// <summary>Traduction des erreurs Identity en erreurs de validation (400) ou de conflit (409).</summary>
internal static class IdentityResults
{
    public static async Task EnsureSucceededAsync(Func<Task<IdentityResult>> action, string defaultField)
    {
        var result = await action();
        if (result.Succeeded)
        {
            return;
        }

        if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
        {
            throw new ConflictException("Un compte existe déjà avec cette adresse e-mail.", "email_taken");
        }

        var failures = result.Errors
            .Select(e => new ValidationFailure(FieldFor(e.Code, defaultField), Translate(e)))
            .ToList();
        throw new ValidationException(failures);
    }

    private static string FieldFor(string code, string defaultField) =>
        code.StartsWith("Password", StringComparison.Ordinal) ? defaultField
        : code.Contains("Email", StringComparison.Ordinal) || code.Contains("UserName", StringComparison.Ordinal) ? "email"
        : defaultField;

    private static string Translate(IdentityError error) => error.Code switch
    {
        "PasswordTooShort" => "Le mot de passe est trop court.",
        "PasswordRequiresDigit" => "Le mot de passe doit contenir au moins un chiffre.",
        "PasswordRequiresUpper" => "Le mot de passe doit contenir au moins une majuscule.",
        "PasswordRequiresLower" => "Le mot de passe doit contenir au moins une minuscule.",
        "PasswordRequiresNonAlphanumeric" => "Le mot de passe doit contenir au moins un caractère spécial.",
        "InvalidEmail" or "InvalidUserName" => "Adresse e-mail invalide.",
        _ => error.Description,
    };
}
