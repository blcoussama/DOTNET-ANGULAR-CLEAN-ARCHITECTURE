using CreditFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Infrastructure.Identity;

/// <summary>
/// Vérifie, à chaque requête authentifiée, que le compte est toujours actif et que son security stamp n'a pas changé
/// (désactivation, changement de rôle) : un jeton d'accès encore valide en durée est alors refusé.
/// </summary>
public sealed class UserSessionValidator(CreditFlowDbContext db)
{
    public async Task<bool> IsValidAsync(Guid userId, string stampFingerprint, CancellationToken cancellationToken)
    {
        var state = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.SecurityStamp })
            .FirstOrDefaultAsync(cancellationToken);
        return state is { IsActive: true }
            && string.Equals(TokenService.StampFingerprint(state.SecurityStamp), stampFingerprint, StringComparison.Ordinal);
    }
}
