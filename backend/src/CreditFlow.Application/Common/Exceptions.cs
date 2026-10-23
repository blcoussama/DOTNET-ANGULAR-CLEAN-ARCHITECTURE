namespace CreditFlow.Application.Common;

/// <summary>Ressource absente ou non accessible à l'appelant (traduite en 404, sans révéler son existence).</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Action interdite pour l'appelant authentifié (traduite en 403).</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>Conflit avec l'état courant (traduit en 409).</summary>
public sealed class ConflictException(string message, string code = "conflict", Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

/// <summary>Compte temporairement verrouillé après trop d'échecs (traduit en 423 Locked).</summary>
public sealed class AccountLockedException(string message) : Exception(message)
{
    public string Code { get; } = "account_locked";
}

/// <summary>Échec d'authentification (traduit en 401).</summary>
public sealed class AuthenticationFailedException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}
