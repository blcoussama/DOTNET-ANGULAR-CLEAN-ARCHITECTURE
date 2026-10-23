namespace CreditFlow.Application.Abstractions;

/// <summary>Stockage binaire des pièces, hors racine web, sous des clés opaques aléatoires.</summary>
public interface IFileStorage
{
    /// <summary>Enregistre le contenu et renvoie la clé générée.</summary>
    Task<string> SaveAsync(Stream content, CancellationToken cancellationToken);

    /// <summary>Ouvre le contenu en lecture, ou null si la clé est inconnue.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
