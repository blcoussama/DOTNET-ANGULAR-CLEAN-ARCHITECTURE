namespace CreditFlow.Domain.Common;

/// <summary>Base des erreurs métier levées par le domaine.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }
}

/// <summary>Règle métier violée par l'état courant de l'agrégat (traduite en 409).</summary>
public class DomainConflictException : DomainException
{
    public DomainConflictException(string message, string code = "conflict")
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Donnée d'entrée invalide au regard d'un invariant du domaine (traduite en 400).</summary>
public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string field, string message)
        : base(message)
    {
        Field = field;
    }

    public string Field { get; }
}
