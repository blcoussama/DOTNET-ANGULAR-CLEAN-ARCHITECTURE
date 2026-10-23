namespace CreditFlow.Domain.Applications;

/// <summary>Statut d'une demande (sérialisé DRAFT, SUBMITTED, UNDER_REVIEW, INFO_REQUESTED, APPROVED, REJECTED, CANCELLED).</summary>
public enum ApplicationStatus
{
    Draft,
    Submitted,
    UnderReview,
    InfoRequested,
    Approved,
    Rejected,
    Cancelled,
}
