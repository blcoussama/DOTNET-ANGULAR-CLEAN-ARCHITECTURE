using CreditFlow.Domain.Common;

namespace CreditFlow.Application.Common;

/// <summary>Utilisateur authentifié à l'origine d'un cas d'usage.</summary>
public sealed record CurrentActor(Guid UserId, string Role)
{
    public bool IsAdmin => Role == Roles.Admin;

    public bool IsReviewer => Roles.IsReviewer(Role);
}
