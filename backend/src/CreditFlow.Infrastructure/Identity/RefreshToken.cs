namespace CreditFlow.Infrastructure.Identity;

/// <summary>
/// Refresh token rotatif. Seule l'empreinte SHA-256 est stockée. Tous les jetons issus d'une même
/// connexion partagent une famille : la réutilisation d'un jeton déjà consommé révoque toute la famille.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid FamilyId { get; set; }

    /// <summary>SHA-256 hexadécimal (64 caractères) du jeton en clair.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Renseigné lors de la rotation ou de la révocation ; sert aussi de jeton de concurrence.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevocationReason { get; set; }

    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}

public static class RevocationReasons
{
    public const string Rotated = "rotated";
    public const string Reused = "reuse_detected";
    public const string Logout = "logout";
    public const string AccountDisabled = "account_disabled";
    public const string RoleChanged = "role_changed";
}
