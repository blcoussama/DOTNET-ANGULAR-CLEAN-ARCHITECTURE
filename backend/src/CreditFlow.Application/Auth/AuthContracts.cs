namespace CreditFlow.Application.Auth;

public sealed record RegisterRequest(string? Email, string? Password, string? FirstName, string? LastName);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record UserDto(Guid Id, string Email, string FirstName, string LastName, string Role);

/// <summary>Réponse de connexion / rafraîchissement ; <c>expiresIn</c> en secondes.</summary>
public sealed record AuthResponse(string AccessToken, int ExpiresIn, UserDto User);

/// <summary>Session émise : réponse JSON + refresh token en clair (destiné uniquement au cookie httpOnly).</summary>
public sealed record AuthSession(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Rotation du refresh token ; un jeton déjà utilisé révoque toute sa famille.</summary>
    Task<AuthSession> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);

    Task<UserDto> GetUserAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Règles de mot de passe (identiques côté Identity et côté validation).</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;
    public const string Description = "8 caractères minimum, dont une majuscule, une minuscule, un chiffre et un caractère spécial.";
}
