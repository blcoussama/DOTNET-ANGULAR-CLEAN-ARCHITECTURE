namespace CreditFlow.Domain.Clients;

/// <summary>Situation professionnelle (sérialisée CDI, CDD, FONCTIONNAIRE, INDEPENDANT, RETRAITE, SANS_EMPLOI).</summary>
public enum EmploymentType
{
    Cdi,
    Cdd,
    Fonctionnaire,
    Independant,
    Retraite,
    SansEmploi,
}
