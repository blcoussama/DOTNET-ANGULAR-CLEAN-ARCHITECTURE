using CreditFlow.Domain.Common;

namespace CreditFlow.Domain.Applications;

/// <summary>Transition absente de la machine à états (traduite en 409).</summary>
public sealed class InvalidStatusTransitionException : DomainConflictException
{
    public InvalidStatusTransitionException(ApplicationStatus from, ApplicationAction action)
        : base(
            $"Action « {ApplicationStateMachine.Describe(action)} » impossible : la demande est au statut « {ApplicationStateMachine.Describe(from)} ».",
            "invalid_transition")
    {
        From = from;
        Action = action;
    }

    public ApplicationStatus From { get; }

    public ApplicationAction Action { get; }
}
