namespace CreditFlow.Domain.Applications;

/// <summary>Entrée d'historique (audit) : immuable une fois créée.</summary>
public sealed class ApplicationEvent
{
    private ApplicationEvent()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    /// <summary>Rang de l'événement dans l'historique de la demande (1, 2, 3…), pour un ordre stable.</summary>
    public int Sequence { get; private set; }

    public ApplicationStatus? FromStatus { get; private set; }

    public ApplicationStatus ToStatus { get; private set; }

    public Guid ActorId { get; private set; }

    public string ActorRole { get; private set; } = string.Empty;

    public string? Comment { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    internal static ApplicationEvent Record(
        Guid applicationId,
        int sequence,
        ApplicationStatus? from,
        ApplicationStatus to,
        Guid actorId,
        string actorRole,
        string? comment,
        DateTimeOffset occurredAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            Sequence = sequence,
            FromStatus = from,
            ToStatus = to,
            ActorId = actorId,
            ActorRole = actorRole,
            Comment = comment,
            OccurredAt = occurredAt,
        };
}
