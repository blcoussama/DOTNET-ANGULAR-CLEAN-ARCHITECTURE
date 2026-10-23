namespace CreditFlow.Application.Documents;

/// <summary>
/// Détection du type réel d'un fichier par sa signature binaire (« magic bytes »),
/// indépendamment de l'extension et du Content-Type déclarés par le client.
/// </summary>
public static class FileSignatures
{
    public const string Pdf = "application/pdf";
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";

    /// <summary>Nombre d'octets nécessaires pour reconnaître tous les formats acceptés.</summary>
    public const int HeaderLength = 8;

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static IReadOnlyList<string> AllowedContentTypes { get; } = [Pdf, Jpeg, Png];

    /// <summary>Renvoie le type MIME détecté, ou null si le format n'est pas autorisé.</summary>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(PdfSignature))
        {
            return Pdf;
        }

        if (header.StartsWith(PngSignature))
        {
            return Png;
        }

        return header.StartsWith(JpegSignature) ? Jpeg : null;
    }

    public static string ExtensionFor(string contentType) => contentType switch
    {
        Pdf => ".pdf",
        Png => ".png",
        Jpeg => ".jpg",
        _ => string.Empty,
    };
}
