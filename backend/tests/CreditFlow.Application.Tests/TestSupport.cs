using CreditFlow.Application.Abstractions;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Common;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Application.Tests;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    public static CreditProduct Product(ProductType type) => CreditProduct.Defaults(Now).Single(p => p.Type == type);

    public static ClientProfile CompleteProfile(Guid userId)
    {
        var profile = ClientProfile.CreateFor(userId, Now);
        profile.Update(new DateOnly(1990, 1, 1), "0600000000", "Tanger", EmploymentType.Cdi, "ACME", 10_000m, 1_000m, 6, Now);
        return profile;
    }

    public static CreditApplication Draft(Guid clientId, decimal amount = 100_000m, int months = 60) =>
        CreditApplication.CreateDraft("CF-2026-000042", clientId, ProductType.Consommation, amount, months, "Travaux", Now.AddDays(-1));

    public static CreditApplication DraftWithDocuments(Guid clientId)
    {
        var application = Draft(clientId);
        application.AddDocument(DocumentType.IdCard, "cin.pdf", "application/pdf", 100, "2026/10/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", clientId, Now);
        application.AddDocument(DocumentType.Payslip, "bulletin.pdf", "application/pdf", 100, "2026/10/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", clientId, Now);
        return application;
    }

    public static CreditApplication Submitted(Guid clientId)
    {
        var application = DraftWithDocuments(clientId);
        application.Submit(
            RiskScoring.Assess(new ScoringInput(ProductType.Consommation, 100_000m, 60, 7.5m, 10_000m, 1_000m, EmploymentType.Cdi, 6)),
            clientId,
            Now);
        return application;
    }

    public static CreditApplication UnderReview(Guid clientId, Guid analystId)
    {
        var application = Submitted(clientId);
        application.AssignTo(analystId, Roles.Analyst, Now);
        return application;
    }

    public static IReadOnlyDictionary<Guid, UserSummary> Users(params (Guid Id, string First, string Last)[] users) =>
        users.ToDictionary(u => u.Id, u => new UserSummary(u.Id, $"{u.First.ToLowerInvariant()}@test.local", u.First, u.Last));
}
