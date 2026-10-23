namespace CreditFlow.Domain.Applications;

/// <summary>Nature d'une pièce justificative (sérialisée ID_CARD, PAYSLIP, BANK_STATEMENT, OTHER).</summary>
public enum DocumentType
{
    IdCard,
    Payslip,
    BankStatement,
    Other,
}
