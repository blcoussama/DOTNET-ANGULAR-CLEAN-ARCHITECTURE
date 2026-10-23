using CreditFlow.Domain.Common;

namespace CreditFlow.Domain.Applications;

/// <summary>Pièce justificative rattachée à une demande (le fichier vit dans le stockage, sous un nom aléatoire).</summary>
public sealed class ApplicationDocument
{
    public const long MaxSizeBytes = 5L * 1024 * 1024;
    public const int MaxFileNameLength = 255;

    private ApplicationDocument()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public DocumentType Type { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>Clé opaque dans le stockage (jamais le nom fourni par le client).</summary>
    public string StorageKey { get; private set; } = string.Empty;

    public Guid UploadedById { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    internal static ApplicationDocument Create(
        Guid applicationId,
        DocumentType type,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        Guid uploadedById,
        DateTimeOffset now)
    {
        if (sizeBytes is <= 0 or > MaxSizeBytes)
        {
            throw new DomainValidationException("file", "Le fichier doit faire entre 1 octet et 5 Mo.");
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new DomainValidationException(nameof(storageKey), "Clé de stockage obligatoire.");
        }

        return new ApplicationDocument
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            Type = type,
            FileName = SanitizeFileName(fileName),
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            UploadedById = uploadedById,
            UploadedAt = now,
        };
    }

    /// <summary>Ne conserve que le nom (sans chemin), sans caractères de contrôle, tronqué à 255 caractères.</summary>
    public static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        var cleaned = new string([.. name.Where(c => !char.IsControl(c) && c is not ('"' or '<' or '>' or '|' or ':' or '*' or '?'))]).Trim();
        if (cleaned.Length == 0)
        {
            cleaned = "document";
        }

        return cleaned.Length <= MaxFileNameLength ? cleaned : cleaned[..MaxFileNameLength];
    }
}
