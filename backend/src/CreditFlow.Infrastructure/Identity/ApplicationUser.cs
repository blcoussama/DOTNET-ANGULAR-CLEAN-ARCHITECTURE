using Microsoft.AspNetCore.Identity;

namespace CreditFlow.Infrastructure.Identity;

/// <summary>Compte utilisateur (ASP.NET Core Identity, clé GUID).</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>Un compte désactivé ne peut plus se connecter et ses jetons sont révoqués.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
