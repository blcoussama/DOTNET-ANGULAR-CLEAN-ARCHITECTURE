using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Common;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Domain.Tests;

public sealed class CreditApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly Guid AnalystId = Guid.NewGuid();

    private static RiskAssessment Assessment() =>
        RiskScoring.Assess(new ScoringInput(ProductType.Consommation, 100_000m, 60, 7.5m, 10_000m, 1_000m, EmploymentType.Cdi, 6));

    private static CreditApplication Draft() =>
        CreditApplication.CreateDraft("CF-2026-000001", ClientId, ProductType.Consommation, 100_000m, 60, "Travaux", Now);

    private static CreditApplication WithDocuments()
    {
        var application = Draft();
        application.AddDocument(DocumentType.IdCard, "cin.pdf", "application/pdf", 1000, "2026/10/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", ClientId, Now);
        application.AddDocument(DocumentType.Payslip, "bulletin.pdf", "application/pdf", 1000, "2026/10/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", ClientId, Now);
        return application;
    }

    private static CreditApplication Submitted()
    {
        var application = WithDocuments();
        application.Submit(Assessment(), ClientId, Now.AddMinutes(1));
        return application;
    }

    private static CreditApplication UnderReview()
    {
        var application = Submitted();
        application.AssignTo(AnalystId, Roles.Analyst, Now.AddMinutes(2));
        return application;
    }

    [Fact]
    public void CreateDraft_records_the_initial_event_and_a_version()
    {
        var application = Draft();

        application.Status.ShouldBe(ApplicationStatus.Draft);
        application.Version.ShouldNotBe(Guid.Empty);
        application.Events.Count.ShouldBe(1);
        var created = application.Events.Single();
        created.FromStatus.ShouldBeNull();
        created.ToStatus.ShouldBe(ApplicationStatus.Draft);
        created.ActorId.ShouldBe(ClientId);
        created.ActorRole.ShouldBe(Roles.Client);
        application.HasBeenSubmitted.ShouldBeFalse();
    }

    [Theory]
    [InlineData("CF-26-000001")]
    [InlineData("XX-2026-000001")]
    [InlineData("CF-2026-1")]
    public void CreateDraft_rejects_a_malformed_reference(string reference) =>
        Should.Throw<DomainValidationException>(() =>
            CreditApplication.CreateDraft(reference, ClientId, ProductType.Auto, 50_000m, 24, null, Now));

    [Fact]
    public void CreateDraft_rejects_a_purpose_longer_than_500_characters() =>
        Should.Throw<DomainValidationException>(() =>
            CreditApplication.CreateDraft("CF-2026-000002", ClientId, ProductType.Auto, 50_000m, 24, new string('x', 501), Now)).Field.ShouldBe("purpose");

    [Fact]
    public void UpdateTerms_changes_the_version()
    {
        var application = Draft();
        var before = application.Version;

        application.UpdateTerms(ProductType.Auto, 80_000m, 48, "  Voiture  ", Now.AddHours(1));

        application.Version.ShouldNotBe(before);
        application.Amount.ShouldBe(80_000m);
        application.Purpose.ShouldBe("Voiture");
        application.UpdatedAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void UpdateTerms_is_refused_once_submitted()
    {
        var ex = Should.Throw<DomainConflictException>(() => Submitted().UpdateTerms(ProductType.Auto, 80_000m, 48, null, Now));
        ex.Code.ShouldBe("not_editable");
    }

    [Fact]
    public void Submit_requires_an_id_card_and_a_payslip()
    {
        var ex = Should.Throw<DomainValidationException>(() => Draft().Submit(Assessment(), ClientId, Now));
        ex.Field.ShouldBe("documents");
    }

    [Fact]
    public void Submit_freezes_rate_payment_debt_ratio_and_score()
    {
        var application = Submitted();

        application.Status.ShouldBe(ApplicationStatus.Submitted);
        application.AnnualRate.ShouldBe(7.5m);
        application.MonthlyPayment.ShouldBe(2003.79m);
        application.TotalCost.ShouldBe(20227.40m);
        application.DebtRatio.ShouldBe(0.3004m);
        application.RiskScore.ShouldBe(97);
        application.RiskGrade.ShouldBe(RiskGrade.A);
        application.Recommendation.ShouldBe(Recommendation.Favorable);
        application.SubmittedAt.ShouldBe(Now.AddMinutes(1));
        application.Events.Last().FromStatus.ShouldBe(ApplicationStatus.Draft);
        application.Events.Last().ToStatus.ShouldBe(ApplicationStatus.Submitted);
    }

    [Fact]
    public void GetAssessment_rebuilds_the_frozen_scoring()
    {
        Draft().GetAssessment().ShouldBeNull();
        Submitted().GetAssessment().ShouldBe(Assessment());
    }

    [Fact]
    public void Submit_by_another_user_is_refused() =>
        Should.Throw<DomainConflictException>(() => WithDocuments().Submit(Assessment(), Guid.NewGuid(), Now)).Code.ShouldBe("not_owner");

    [Fact]
    public void Assign_moves_to_under_review_and_records_the_analyst()
    {
        var application = UnderReview();

        application.Status.ShouldBe(ApplicationStatus.UnderReview);
        application.AssignedAnalystId.ShouldBe(AnalystId);
        application.Events.Last().ActorRole.ShouldBe(Roles.Analyst);
    }

    [Fact]
    public void Assign_by_a_client_is_refused() =>
        Should.Throw<DomainConflictException>(() => Submitted().AssignTo(ClientId, Roles.Client, Now));

    [Fact]
    public void Approve_requires_a_comment() =>
        Should.Throw<DomainValidationException>(() => UnderReview().Approve(AnalystId, Roles.Analyst, "   ", Now)).Field.ShouldBe("comment");

    [Fact]
    public void Approve_rejects_a_comment_over_1000_characters() =>
        Should.Throw<DomainValidationException>(() => UnderReview().Approve(AnalystId, Roles.Analyst, new string('a', 1001), Now));

    [Fact]
    public void Approve_by_the_assigned_analyst_sets_the_decision()
    {
        var application = UnderReview();
        var decidedAt = Now.AddDays(2);

        application.Approve(AnalystId, Roles.Analyst, " Dossier solide ", decidedAt);

        application.Status.ShouldBe(ApplicationStatus.Approved);
        application.DecisionComment.ShouldBe("Dossier solide");
        application.DecidedAt.ShouldBe(decidedAt);
        application.Events.Last().Comment.ShouldBe("Dossier solide");
    }

    [Fact]
    public void Another_analyst_cannot_decide()
    {
        var application = UnderReview();

        application.CanBeDecidedBy(Guid.NewGuid(), Roles.Analyst).ShouldBeFalse();
        Should.Throw<DomainConflictException>(() => application.Reject(Guid.NewGuid(), Roles.Analyst, "Non", Now)).Code.ShouldBe("not_assigned");
    }

    [Fact]
    public void An_admin_can_decide_without_being_assigned()
    {
        var application = UnderReview();

        application.Reject(Guid.NewGuid(), Roles.Admin, "Endettement excessif", Now.AddDays(1));

        application.Status.ShouldBe(ApplicationStatus.Rejected);
        application.DecidedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Request_info_then_resubmission_goes_back_to_submitted()
    {
        var application = UnderReview();
        application.RequestInformation(AnalystId, Roles.Analyst, "Relevés bancaires", Now.AddDays(1));

        application.Status.ShouldBe(ApplicationStatus.InfoRequested);
        application.IsEditable.ShouldBeTrue();
        application.DecisionComment.ShouldBeNull();
        application.DecidedAt.ShouldBeNull();

        application.Submit(Assessment(), ClientId, Now.AddDays(2));

        application.Status.ShouldBe(ApplicationStatus.Submitted);
        application.SubmittedAt.ShouldBe(Now.AddDays(2));
        application.Events.Select(e => e.ToStatus).ShouldBe(
        [
            ApplicationStatus.Draft,
            ApplicationStatus.Submitted,
            ApplicationStatus.UnderReview,
            ApplicationStatus.InfoRequested,
            ApplicationStatus.Submitted,
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Client_can_cancel_a_draft_or_a_submitted_application(bool submitted)
    {
        var application = submitted ? Submitted() : Draft();

        application.Cancel(ClientId, "Projet abandonné", Now.AddDays(1));

        application.Status.ShouldBe(ApplicationStatus.Cancelled);
        application.Events.Last().Comment.ShouldBe("Projet abandonné");
    }

    [Fact]
    public void Cancel_is_refused_under_review() =>
        Should.Throw<InvalidStatusTransitionException>(() => UnderReview().Cancel(ClientId, null, Now));

    [Fact]
    public void Cancel_by_another_user_is_refused() =>
        Should.Throw<DomainConflictException>(() => Draft().Cancel(Guid.NewGuid(), null, Now));

    [Fact]
    public void Each_transition_regenerates_the_version()
    {
        var application = WithDocuments();
        var versions = new HashSet<Guid> { application.Version };

        application.Submit(Assessment(), ClientId, Now);
        versions.Add(application.Version).ShouldBeTrue();
        application.AssignTo(AnalystId, Roles.Analyst, Now);
        versions.Add(application.Version).ShouldBeTrue();
    }

    [Fact]
    public void EnsureVersion_detects_a_stale_version()
    {
        var application = Draft();

        Should.NotThrow(() => application.EnsureVersion(null));
        Should.NotThrow(() => application.EnsureVersion(application.Version));
        Should.Throw<DomainConflictException>(() => application.EnsureVersion(Guid.NewGuid())).Code.ShouldBe("concurrency_conflict");
    }

    [Fact]
    public void Documents_cannot_be_added_once_submitted() =>
        Should.Throw<DomainConflictException>(() =>
            Submitted().AddDocument(DocumentType.Other, "x.pdf", "application/pdf", 10, "2026/10/cccccccccccccccccccccccccccccccc", ClientId, Now));

    [Fact]
    public void Documents_are_limited_to_twenty()
    {
        var application = Draft();
        for (var i = 0; i < CreditApplication.MaxDocuments; i++)
        {
            application.AddDocument(DocumentType.Other, $"{i}.pdf", "application/pdf", 10, $"2026/10/{i:D32}", ClientId, Now);
        }

        Should.Throw<DomainValidationException>(() =>
            application.AddDocument(DocumentType.Other, "21.pdf", "application/pdf", 10, "2026/10/dddddddddddddddddddddddddddddddd", ClientId, Now));
    }

    [Fact]
    public void RemoveDocument_returns_the_removed_document()
    {
        var application = WithDocuments();
        var idCard = application.Documents.First(d => d.Type == DocumentType.IdCard);

        var removed = application.RemoveDocument(idCard.Id, Now);

        removed.ShouldBe(idCard);
        application.HasDocument(DocumentType.IdCard).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5L * 1024 * 1024 + 1)]
    public void Document_size_must_be_between_one_byte_and_5_MB(long size) =>
        Should.Throw<DomainValidationException>(() =>
            Draft().AddDocument(DocumentType.Other, "x.pdf", "application/pdf", size, "2026/10/eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", ClientId, Now));

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\Users\\moi\\cin.pdf", "cin.pdf")]
    [InlineData("bul\u0000let\"in<>.pdf", "bulletin.pdf")]
    [InlineData("   ", "document")]
    [InlineData(null, "document")]
    public void File_names_are_sanitized(string? input, string expected) =>
        ApplicationDocument.SanitizeFileName(input).ShouldBe(expected);

    [Fact]
    public void Long_file_names_are_truncated() =>
        ApplicationDocument.SanitizeFileName(new string('a', 300) + ".pdf").Length.ShouldBe(255);
}
