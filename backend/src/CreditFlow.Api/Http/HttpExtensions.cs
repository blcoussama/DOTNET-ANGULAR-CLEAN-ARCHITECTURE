using System.Security.Claims;
using CreditFlow.Application.Common;
using CreditFlow.Infrastructure.Identity;

namespace CreditFlow.Api.Http;

internal static class HttpExtensions
{
    /// <summary>Utilisateur courant issu des claims du JWT (sub, role).</summary>
    public static CurrentActor ToActor(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(CreditFlowClaims.Subject);
        var role = principal.FindFirstValue(CreditFlowClaims.Role);
        return Guid.TryParse(subject, out var id) && role is not null
            ? new CurrentActor(id, role)
            : throw new AuthenticationFailedException("Jeton d'accès invalide.", "invalid_token");
    }

    public static Guid UserId(this ClaimsPrincipal principal) => principal.ToActor().UserId;

    /// <summary>Version attendue lue dans l'en-tête facultatif <c>If-Match</c> (« "guid" », « W/"guid" » ou « guid »).</summary>
    public static Guid? ExpectedVersion(this HttpRequest request)
    {
        var raw = request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "*")
        {
            return null;
        }

        var value = raw.Trim();
        if (value.StartsWith("W/", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        value = value.Trim('"');
        return Guid.TryParse(value, out var version)
            ? version
            : throw ValidationExtensions.ValidationError("If-Match", "En-tête If-Match invalide : la version attendue est un identifiant GUID.");
    }

    public static void SetVersionHeader(this HttpResponse response, Guid version) =>
        response.Headers.ETag = $"\"{version}\"";
}
