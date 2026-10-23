namespace CreditFlow.Domain.Applications;

/// <summary>Actions déclenchant une transition de la machine à états.</summary>
public enum ApplicationAction
{
    Submit,
    Assign,
    Approve,
    Reject,
    RequestInfo,
    Cancel,
}
