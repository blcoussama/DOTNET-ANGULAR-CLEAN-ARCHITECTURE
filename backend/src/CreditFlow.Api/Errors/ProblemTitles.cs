namespace CreditFlow.Api.Errors;

/// <summary>Titres ProblemDetails en français, par code HTTP.</summary>
internal static class ProblemTitles
{
    public static string For(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Requête invalide",
        StatusCodes.Status401Unauthorized => "Authentification requise",
        StatusCodes.Status403Forbidden => "Accès refusé",
        StatusCodes.Status404NotFound => "Ressource introuvable",
        StatusCodes.Status405MethodNotAllowed => "Méthode non autorisée",
        StatusCodes.Status409Conflict => "Conflit",
        StatusCodes.Status413PayloadTooLarge => "Contenu trop volumineux",
        StatusCodes.Status415UnsupportedMediaType => "Type de contenu non pris en charge",
        StatusCodes.Status423Locked => "Compte verrouillé",
        StatusCodes.Status429TooManyRequests => "Trop de requêtes",
        StatusCodes.Status500InternalServerError => "Erreur interne",
        StatusCodes.Status503ServiceUnavailable => "Service indisponible",
        _ => "Erreur",
    };
}
