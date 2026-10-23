using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Applications;
using CreditFlow.Application.Common;
using CreditFlow.Domain.Applications;
using Microsoft.Extensions.Logging;

namespace CreditFlow.Application.Documents;

/// <summary>Contenu d'une pièce prêt à être renvoyé au navigateur.</summary>
public sealed record DocumentContent(Stream Content, string ContentType, string FileName, long SizeBytes);

/// <summary>Dépôt, téléchargement et suppression des pièces justificatives.</summary>
public sealed class DocumentService(
    ICreditApplicationRepository applications,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<DocumentService> logger)
{
    private const int MaxUploadAttempts = 5;
    private const string ConcurrencyConflictCode = "concurrency_conflict";

    public async Task<DocumentDto> UploadAsync(
        Guid clientId,
        Guid applicationId,
        DocumentType? type,
        string? fileName,
        Stream? content,
        CancellationToken cancellationToken)
    {
        var application = await applications.GetAsync(applicationId, cancellationToken);
        if (application is null || application.ClientId != clientId)
        {
            throw new NotFoundException("Demande introuvable.");
        }

        if (type is null || !Enum.IsDefined(type.Value))
        {
            throw ValidationExtensions.ValidationError("type", $"Le type de pièce est obligatoire ({EnumWire.AllowedValues<DocumentType>()}).");
        }

        if (content is null)
        {
            throw ValidationExtensions.ValidationError("file", "Le fichier est obligatoire.");
        }

        // Copie bornée : on ne lit jamais plus de 5 Mo + 1 octet, quelle que soit la taille annoncée.
        await using var buffer = new MemoryStream();
        var tooLarge = await CopyBoundedAsync(content, buffer, ApplicationDocument.MaxSizeBytes, cancellationToken);
        if (tooLarge)
        {
            throw ValidationExtensions.ValidationError("file", "Le fichier dépasse la taille maximale autorisée (5 Mo).");
        }

        if (buffer.Length == 0)
        {
            throw ValidationExtensions.ValidationError("file", "Le fichier est vide.");
        }

        var header = buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, FileSignatures.HeaderLength));
        var contentType = FileSignatures.Detect(header)
            ?? throw ValidationExtensions.ValidationError("file", "Format non autorisé : seuls les fichiers PDF, JPEG et PNG sont acceptés (signature binaire vérifiée).");

        if (!application.IsEditable)
        {
            throw new ConflictException(
                $"Impossible d'ajouter une pièce à une demande au statut « {ApplicationStateMachine.Describe(application.Status)} ».",
                "not_editable");
        }

        buffer.Position = 0;
        var storageKey = await storage.SaveAsync(buffer, cancellationToken);
        try
        {
            // Pièces envoyées en parallèle sur la même demande : le jeton de concurrence de l'agrégat en fait
            // échouer une. On recharge la demande et on rejoue l'ajout (statut et nombre de pièces revérifiés).
            for (var attempt = 1; ; attempt++)
            {
                var document = application.AddDocument(
                    type.Value,
                    fileName ?? "document",
                    contentType,
                    buffer.Length,
                    storageKey,
                    clientId,
                    clock.GetUtcNow());
                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return ApplicationMapper.ToDto(document);
                }
                catch (ConflictException ex) when (ex.Code == ConcurrencyConflictCode && attempt < MaxUploadAttempts)
                {
                    unitOfWork.DiscardChanges();
                    application = await applications.GetAsync(applicationId, cancellationToken)
                        ?? throw new NotFoundException("Demande introuvable.");
                }
            }
        }
        catch
        {
            // Compensation : pas de fichier orphelin si l'enregistrement échoue.
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }
    }

    /// <summary>Téléchargement réservé au client titulaire, ou à un analyste / admin pour une demande soumise.</summary>
    public async Task<DocumentContent> OpenAsync(CurrentActor actor, Guid applicationId, Guid documentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var application = await applications.GetAsync(applicationId, cancellationToken);
        var allowed = application is not null
            && (application.ClientId == actor.UserId || (actor.IsReviewer && application.HasBeenSubmitted));
        var document = allowed ? application!.Documents.FirstOrDefault(d => d.Id == documentId) : null;
        if (document is null)
        {
            throw new NotFoundException("Pièce introuvable.");
        }

        var stream = await storage.OpenReadAsync(document.StorageKey, cancellationToken);
        if (stream is null)
        {
            logger.LogError("Fichier manquant dans le stockage pour la pièce {DocumentId}", document.Id);
            throw new NotFoundException("Pièce introuvable.");
        }

        return new DocumentContent(stream, document.ContentType, document.FileName, document.SizeBytes);
    }

    public async Task DeleteAsync(Guid clientId, Guid applicationId, Guid documentId, CancellationToken cancellationToken)
    {
        var application = await applications.GetAsync(applicationId, cancellationToken);
        if (application is null || application.ClientId != clientId || application.Documents.All(d => d.Id != documentId))
        {
            throw new NotFoundException("Pièce introuvable.");
        }

        var removed = application.RemoveDocument(documentId, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(removed.StorageKey, cancellationToken);
    }

    private static async Task<bool> CopyBoundedAsync(Stream source, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                return true;
            }

            await destination.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return false;
    }
}
