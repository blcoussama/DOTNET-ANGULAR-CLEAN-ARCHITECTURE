using System.Text.RegularExpressions;
using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Common;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Domain.Applications;

/// <summary>
/// Agrégat « demande de crédit ». Toutes les transitions passent par <see cref="ApplicationStateMachine"/>
/// et produisent un <see cref="ApplicationEvent"/>. <see cref="Version"/> sert de jeton de concurrence portable.
/// </summary>
public sealed partial class CreditApplication
{
    public const int MaxPurposeLength = 500;
    public const int MaxCommentLength = 1000;
    public const int MaxDocuments = 20;

    private readonly List<ApplicationDocument> _documents = [];
    private readonly List<ApplicationEvent> _events = [];

    private CreditApplication()
    {
    }

    public Guid Id { get; private set; }

    public string Reference { get; private set; } = string.Empty;

    public Guid ClientId { get; private set; }

    public ProductType ProductType { get; private set; }

    public decimal Amount { get; private set; }

    public int DurationMonths { get; private set; }

    public string? Purpose { get; private set; }

    public ApplicationStatus Status { get; private set; }

    // --- Données figées à la soumission ---
    public decimal? AnnualRate { get; private set; }

    public decimal? MonthlyPayment { get; private set; }

    public decimal? TotalCost { get; private set; }

    public decimal? DebtRatio { get; private set; }

    public int? RiskScore { get; private set; }

    public RiskGrade? RiskGrade { get; private set; }

    public Recommendation? Recommendation { get; private set; }

    public decimal? AmountToAnnualIncomeRatio { get; private set; }

    public int? DebtRatioPoints { get; private set; }

    public int? EmploymentPoints { get; private set; }

    public int? SeniorityPoints { get; private set; }

    public int? AmountToIncomePoints { get; private set; }

    public decimal? DebtThreshold { get; private set; }

    public decimal? ScoredMonthlyIncome { get; private set; }

    public decimal? ScoredMonthlyCharges { get; private set; }

    public EmploymentType? ScoredEmploymentType { get; private set; }

    public int? ScoredYearsAtCurrentJob { get; private set; }

    // --- Instruction ---
    public Guid? AssignedAnalystId { get; private set; }

    public string? DecisionComment { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Jeton de concurrence régénéré à chaque modification.</summary>
    public Guid Version { get; private set; }

    public IReadOnlyCollection<ApplicationDocument> Documents => _documents.AsReadOnly();

    public IReadOnlyCollection<ApplicationEvent> Events => _events.AsReadOnly();

    public bool IsEditable => ApplicationStateMachine.IsEditable(Status);

    /// <summary>La demande a déjà été soumise au moins une fois (visible des analystes).</summary>
    public bool HasBeenSubmitted => SubmittedAt is not null;

    public static bool IsValidReference(string reference) => ReferenceRegex().IsMatch(reference);

    public static CreditApplication CreateDraft(
        string reference,
        Guid clientId,
        ProductType productType,
        decimal amount,
        int durationMonths,
        string? purpose,
        DateTimeOffset now)
    {
        if (!IsValidReference(reference))
        {
            throw new DomainValidationException(nameof(reference), "Référence invalide (format attendu CF-AAAA-NNNNNN).");
        }

        if (clientId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(clientId), "Client obligatoire.");
        }

        var application = new CreditApplication
        {
            Id = Guid.CreateVersion7(),
            Reference = reference,
            ClientId = clientId,
            Status = ApplicationStatus.Draft,
            CreatedAt = now,
        };
        application.SetTerms(productType, amount, durationMonths, purpose);
        application._events.Add(ApplicationEvent.Record(application.Id, 1, null, ApplicationStatus.Draft, clientId, Roles.Client, null, now));
        application.Touch(now);
        return application;
    }

    /// <summary>Lève un conflit si la version connue du client ne correspond plus.</summary>
    public void EnsureVersion(Guid? expectedVersion)
    {
        if (expectedVersion is { } expected && expected != Version)
        {
            throw new DomainConflictException(
                "La demande a été modifiée entre-temps. Rechargez-la avant de réessayer.",
                "concurrency_conflict");
        }
    }

    public void UpdateTerms(ProductType productType, decimal amount, int durationMonths, string? purpose, DateTimeOffset now)
    {
        EnsureEditable("modifier");
        SetTerms(productType, amount, durationMonths, purpose);
        Touch(now);
    }

    /// <summary>Soumission (ou resoumission) : fige taux, mensualité, endettement et score.</summary>
    public void Submit(RiskAssessment assessment, Guid actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        var next = ApplicationStateMachine.Next(Status, ApplicationAction.Submit);
        if (actorId != ClientId)
        {
            throw new DomainConflictException("Seul le client titulaire peut soumettre sa demande.", "not_owner");
        }

        if (!HasDocument(DocumentType.IdCard) || !HasDocument(DocumentType.Payslip))
        {
            throw new DomainValidationException("documents", "Une pièce d'identité (ID_CARD) et un bulletin de salaire (PAYSLIP) sont obligatoires.");
        }

        AnnualRate = assessment.AnnualRate;
        MonthlyPayment = assessment.MonthlyPayment;
        TotalCost = assessment.TotalCost;
        DebtRatio = assessment.DebtRatio;
        RiskScore = assessment.Score;
        RiskGrade = assessment.Grade;
        Recommendation = assessment.Recommendation;
        AmountToAnnualIncomeRatio = assessment.AmountToAnnualIncomeRatio;
        DebtRatioPoints = assessment.DebtRatioPoints;
        EmploymentPoints = assessment.EmploymentPoints;
        SeniorityPoints = assessment.SeniorityPoints;
        AmountToIncomePoints = assessment.AmountToIncomePoints;
        DebtThreshold = assessment.DebtThreshold;
        ScoredMonthlyIncome = assessment.MonthlyIncome;
        ScoredMonthlyCharges = assessment.MonthlyCharges;
        ScoredEmploymentType = assessment.EmploymentType;
        ScoredYearsAtCurrentJob = assessment.YearsAtCurrentJob;
        SubmittedAt = now;

        Apply(next, actorId, Roles.Client, null, now);
    }

    public void AssignTo(Guid analystId, string actorRole, DateTimeOffset now)
    {
        var next = ApplicationStateMachine.Next(Status, ApplicationAction.Assign);
        if (!Roles.IsReviewer(actorRole))
        {
            throw new DomainConflictException("Seul un analyste ou un administrateur peut s'assigner un dossier.", "not_reviewer");
        }

        AssignedAnalystId = analystId;
        Apply(next, analystId, actorRole, null, now);
    }

    public void Approve(Guid actorId, string actorRole, string comment, DateTimeOffset now) =>
        Decide(ApplicationAction.Approve, actorId, actorRole, comment, now);

    public void Reject(Guid actorId, string actorRole, string comment, DateTimeOffset now) =>
        Decide(ApplicationAction.Reject, actorId, actorRole, comment, now);

    public void RequestInformation(Guid actorId, string actorRole, string comment, DateTimeOffset now) =>
        Decide(ApplicationAction.RequestInfo, actorId, actorRole, comment, now);

    /// <summary>Un analyste peut décider s'il est assigné ; un administrateur peut toujours décider.</summary>
    public bool CanBeDecidedBy(Guid actorId, string actorRole) =>
        actorRole == Roles.Admin || (actorRole == Roles.Analyst && AssignedAnalystId == actorId);

    public void Cancel(Guid actorId, string? comment, DateTimeOffset now)
    {
        var next = ApplicationStateMachine.Next(Status, ApplicationAction.Cancel);
        if (actorId != ClientId)
        {
            throw new DomainConflictException("Seul le client titulaire peut annuler sa demande.", "not_owner");
        }

        var normalized = NormalizeComment(comment, required: false);
        Apply(next, actorId, Roles.Client, normalized, now);
    }

    public bool HasDocument(DocumentType type) => _documents.Exists(d => d.Type == type);

    public ApplicationDocument AddDocument(
        DocumentType type,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        Guid uploadedById,
        DateTimeOffset now)
    {
        EnsureEditable("ajouter une pièce à");
        if (_documents.Count >= MaxDocuments)
        {
            throw new DomainValidationException("file", $"Une demande ne peut pas comporter plus de {MaxDocuments} pièces.");
        }

        var document = ApplicationDocument.Create(Id, type, fileName, contentType, sizeBytes, storageKey, uploadedById, now);
        _documents.Add(document);
        Touch(now);
        return document;
    }

    public ApplicationDocument RemoveDocument(Guid documentId, DateTimeOffset now)
    {
        EnsureEditable("retirer une pièce de");
        var document = _documents.Find(d => d.Id == documentId)
            ?? throw new DomainValidationException("documentId", "Pièce introuvable.");
        _documents.Remove(document);
        Touch(now);
        return document;
    }

    /// <summary>Reconstitue le résultat du scoring figé (null tant que la demande n'a pas été soumise).</summary>
    public RiskAssessment? GetAssessment()
    {
        if (AnnualRate is not { } rate || MonthlyPayment is not { } payment || TotalCost is not { } cost
            || DebtRatio is not { } debt || AmountToAnnualIncomeRatio is not { } amountRatio
            || DebtRatioPoints is not { } debtPoints || EmploymentPoints is not { } employmentPoints
            || SeniorityPoints is not { } seniorityPoints || AmountToIncomePoints is not { } amountPoints
            || RiskScore is not { } score || RiskGrade is not { } grade || Recommendation is not { } recommendation
            || DebtThreshold is not { } threshold || ScoredMonthlyIncome is not { } income
            || ScoredMonthlyCharges is not { } charges || ScoredEmploymentType is not { } employment
            || ScoredYearsAtCurrentJob is not { } years)
        {
            return null;
        }

        return new RiskAssessment(
            rate, payment, cost, debt, amountRatio, debtPoints, employmentPoints, seniorityPoints, amountPoints,
            score, grade, recommendation, threshold, income, charges, employment, years);
    }

    private void Decide(ApplicationAction action, Guid actorId, string actorRole, string comment, DateTimeOffset now)
    {
        var next = ApplicationStateMachine.Next(Status, action);
        if (!CanBeDecidedBy(actorId, actorRole))
        {
            throw new DomainConflictException("Seul l'analyste assigné ou un administrateur peut statuer sur ce dossier.", "not_assigned");
        }

        var normalized = NormalizeComment(comment, required: true);
        if (action is ApplicationAction.Approve or ApplicationAction.Reject)
        {
            DecisionComment = normalized;
            DecidedAt = now;
        }

        Apply(next, actorId, actorRole, normalized, now);
    }

    private void Apply(ApplicationStatus next, Guid actorId, string actorRole, string? comment, DateTimeOffset now)
    {
        var previous = Status;
        Status = next;
        _events.Add(ApplicationEvent.Record(Id, _events.Count + 1, previous, next, actorId, actorRole, comment, now));
        Touch(now);
    }

    private void EnsureEditable(string verb)
    {
        if (!IsEditable)
        {
            throw new DomainConflictException(
                $"Impossible de {verb} une demande au statut « {ApplicationStateMachine.Describe(Status)} » : seules les demandes en brouillon ou en attente de compléments sont modifiables.",
                "not_editable");
        }
    }

    private void SetTerms(ProductType productType, decimal amount, int durationMonths, string? purpose)
    {
        if (amount <= 0)
        {
            throw new DomainValidationException(nameof(amount), "Le montant doit être strictement positif.");
        }

        if (durationMonths <= 0)
        {
            throw new DomainValidationException(nameof(durationMonths), "La durée doit être strictement positive.");
        }

        var trimmed = string.IsNullOrWhiteSpace(purpose) ? null : purpose.Trim();
        if (trimmed is { Length: > MaxPurposeLength })
        {
            throw new DomainValidationException(nameof(purpose), $"L'objet du crédit est limité à {MaxPurposeLength} caractères.");
        }

        ProductType = productType;
        Amount = amount;
        DurationMonths = durationMonths;
        Purpose = trimmed;
    }

    private static string? NormalizeComment(string? comment, bool required)
    {
        var trimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (required && trimmed is null)
        {
            throw new DomainValidationException("comment", "Un commentaire est obligatoire.");
        }

        if (trimmed is { Length: > MaxCommentLength })
        {
            throw new DomainValidationException("comment", $"Le commentaire est limité à {MaxCommentLength} caractères.");
        }

        return trimmed;
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }

    [GeneratedRegex(@"^CF-\d{4}-\d{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceRegex();
}
