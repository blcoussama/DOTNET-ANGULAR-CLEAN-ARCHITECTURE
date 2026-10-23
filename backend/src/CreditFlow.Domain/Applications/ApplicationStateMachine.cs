namespace CreditFlow.Domain.Applications;

/// <summary>
/// Machine à états du cycle de vie d'une demande. Toute transition absente de la table est interdite.
/// </summary>
public static class ApplicationStateMachine
{
    private static readonly Dictionary<(ApplicationStatus From, ApplicationAction Action), ApplicationStatus> Transitions = new()
    {
        [(ApplicationStatus.Draft, ApplicationAction.Submit)] = ApplicationStatus.Submitted,
        [(ApplicationStatus.InfoRequested, ApplicationAction.Submit)] = ApplicationStatus.Submitted,
        [(ApplicationStatus.Submitted, ApplicationAction.Assign)] = ApplicationStatus.UnderReview,
        [(ApplicationStatus.UnderReview, ApplicationAction.Approve)] = ApplicationStatus.Approved,
        [(ApplicationStatus.UnderReview, ApplicationAction.Reject)] = ApplicationStatus.Rejected,
        [(ApplicationStatus.UnderReview, ApplicationAction.RequestInfo)] = ApplicationStatus.InfoRequested,
        [(ApplicationStatus.Draft, ApplicationAction.Cancel)] = ApplicationStatus.Cancelled,
        [(ApplicationStatus.Submitted, ApplicationAction.Cancel)] = ApplicationStatus.Cancelled,
        [(ApplicationStatus.InfoRequested, ApplicationAction.Cancel)] = ApplicationStatus.Cancelled,
    };

    public static bool CanApply(ApplicationStatus from, ApplicationAction action) =>
        Transitions.ContainsKey((from, action));

    /// <summary>Statut obtenu après l'action ; lève <see cref="InvalidStatusTransitionException"/> si interdite.</summary>
    public static ApplicationStatus Next(ApplicationStatus from, ApplicationAction action) =>
        Transitions.TryGetValue((from, action), out var to)
            ? to
            : throw new InvalidStatusTransitionException(from, action);

    public static IReadOnlyList<ApplicationAction> AllowedActions(ApplicationStatus from) =>
        [.. Transitions.Keys.Where(k => k.From == from).Select(k => k.Action)];

    public static bool IsFinal(ApplicationStatus status) =>
        status is ApplicationStatus.Approved or ApplicationStatus.Rejected or ApplicationStatus.Cancelled;

    /// <summary>Une demande n'est modifiable par le client qu'en brouillon ou en attente de compléments.</summary>
    public static bool IsEditable(ApplicationStatus status) =>
        status is ApplicationStatus.Draft or ApplicationStatus.InfoRequested;

    public static string Describe(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Draft => "Brouillon",
        ApplicationStatus.Submitted => "Soumise",
        ApplicationStatus.UnderReview => "En cours d'étude",
        ApplicationStatus.InfoRequested => "Compléments demandés",
        ApplicationStatus.Approved => "Approuvée",
        ApplicationStatus.Rejected => "Refusée",
        ApplicationStatus.Cancelled => "Annulée",
        _ => status.ToString(),
    };

    public static string Describe(ApplicationAction action) => action switch
    {
        ApplicationAction.Submit => "soumettre",
        ApplicationAction.Assign => "s'assigner",
        ApplicationAction.Approve => "approuver",
        ApplicationAction.Reject => "refuser",
        ApplicationAction.RequestInfo => "demander des compléments",
        ApplicationAction.Cancel => "annuler",
        _ => action.ToString(),
    };
}
