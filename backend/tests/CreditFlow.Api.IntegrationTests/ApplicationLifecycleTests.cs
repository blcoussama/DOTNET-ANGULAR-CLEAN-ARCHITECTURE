using System.Net;
using System.Net.Http.Json;
using CreditFlow.Api.IntegrationTests.Infrastructure;
using CreditFlow.Application.Applications;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Api.IntegrationTests;

[Collection(SharedApiDefinition.Name)]
public sealed class ApplicationLifecycleTests(CreditFlowApiFactory factory)
{
    private readonly ApiDriver _api = new(factory);

    [Fact]
    public async Task Create_draft_returns_201_with_a_readable_reference()
    {
        var client = await _api.RegisterClientAsync();

        var response = await client.Http.PostAsJsonAsync("/api/v1/applications", new { productType = "AUTO", amount = 80_000, durationMonths = 48, purpose = "Voiture" }, ApiDriver.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var draft = await ApiDriver.ReadAsync<ApplicationDetailDto>(response);
        response.Headers.Location!.ToString().ShouldEndWith($"/api/v1/applications/{draft.Id}");
        response.Headers.ETag!.Tag.ShouldBe($"\"{draft.Version}\"");
        draft.Reference.ShouldMatch(@"^CF-\d{4}-\d{6}$");
        draft.Status.ShouldBe(ApplicationStatus.Draft);
        draft.Events.Single().ToStatus.ShouldBe(ApplicationStatus.Draft);
    }

    [Fact]
    public async Task Draft_amount_outside_product_bounds_is_rejected()
    {
        var client = await _api.RegisterClientAsync();

        var response = await client.Http.PostAsJsonAsync("/api/v1/applications", new { productType = "IMMOBILIER", amount = 50_000, durationMonths = 24 }, ApiDriver.Json);

        var problem = await ApiDriver.ProblemAsync(response, HttpStatusCode.BadRequest);
        problem.GetProperty("errors").TryGetProperty("amount", out _).ShouldBeTrue();
        problem.GetProperty("errors").TryGetProperty("durationMonths", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Submit_requires_a_complete_profile_and_mandatory_documents()
    {
        var client = await _api.RegisterClientAsync();
        var draft = await ApiDriver.CreateDraftAsync(client);

        var response = await client.Http.PostAsync($"/api/v1/applications/{draft.Id}/submit", null);

        var errors = (await ApiDriver.ProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        errors.TryGetProperty("profile", out _).ShouldBeTrue();
        errors.GetProperty("documents").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Full_lifecycle_from_draft_to_approval_with_an_information_request()
    {
        var client = await _api.RegisterClientAsync();
        var analyst = await _api.StaffAsync(RoleNames.Analyst);

        // Brouillon → pièces → soumission : le scoring est calculé et figé.
        var submitted = await ApiDriver.SubmittedApplicationAsync(client);
        submitted.Status.ShouldBe(ApplicationStatus.Submitted);
        submitted.AnnualRate.ShouldBe(7.5m);
        submitted.MonthlyPayment.ShouldBe(2003.79m);
        submitted.TotalCost.ShouldBe(20227.40m);
        submitted.DebtRatio.ShouldBe(0.3004m);
        submitted.SubmittedAt.ShouldNotBeNull();

        var assigned = await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "assign");
        assigned.Status.ShouldBe(ApplicationStatus.UnderReview);
        assigned.AssignedAnalystId.ShouldBe(analyst.UserId);
        assigned.RiskScore.ShouldBe(97);
        assigned.RiskGrade.ShouldBe(RiskGrade.A);
        assigned.Recommendation.ShouldBe(Recommendation.Favorable);

        var infoRequested = await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "request-info", "Merci d'ajouter un relevé bancaire.");
        infoRequested.Status.ShouldBe(ApplicationStatus.InfoRequested);

        // Le client complète son dossier, ajuste sa demande puis la resoumet.
        await ApiDriver.UploadOkAsync(client, submitted.Id, "BANK_STATEMENT");
        var update = await client.Http.PutAsJsonAsync($"/api/v1/applications/{submitted.Id}", new { productType = "CONSOMMATION", amount = 90_000, durationMonths = 60, purpose = "Travaux (révisé)" }, ApiDriver.Json);
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resubmit = await client.Http.PostAsync($"/api/v1/applications/{submitted.Id}/submit", null);
        resubmit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resubmitted = await ApiDriver.ReadAsync<ApplicationDetailDto>(resubmit);
        resubmitted.Status.ShouldBe(ApplicationStatus.Submitted);
        resubmitted.Amount.ShouldBe(90_000m);
        resubmitted.MonthlyPayment!.Value.ShouldBeLessThan(2003.79m);

        await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "assign");
        var approved = await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "approve", "Capacité de remboursement confirmée.");
        approved.Status.ShouldBe(ApplicationStatus.Approved);
        approved.DecidedAt.ShouldNotBeNull();
        approved.DecisionComment.ShouldBe("Capacité de remboursement confirmée.");

        var final = await client.Http.GetFromJsonAsync<ApplicationDetailDto>($"/api/v1/applications/{submitted.Id}", ApiDriver.Json);
        final!.Status.ShouldBe(ApplicationStatus.Approved);
        final.Documents.Count.ShouldBe(3);
        final.Events.Select(e => e.ToStatus).ShouldBe(
        [
            ApplicationStatus.Draft,
            ApplicationStatus.Submitted,
            ApplicationStatus.UnderReview,
            ApplicationStatus.InfoRequested,
            ApplicationStatus.Submitted,
            ApplicationStatus.UnderReview,
            ApplicationStatus.Approved,
        ]);
        final.Events[3].Comment.ShouldBe("Merci d'ajouter un relevé bancaire.");
        final.Events[3].ActorRole.ShouldBe("Analyst");
        final.Events[3].ActorName.ShouldBe("Analyst Test");
    }

    [Fact]
    public async Task Rejection_is_recorded_with_its_comment()
    {
        var client = await _api.RegisterClientAsync();
        var analyst = await _api.StaffAsync(RoleNames.Analyst);
        var submitted = await ApiDriver.SubmittedApplicationAsync(client, "AUTO", 250_000m, 84);
        await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "assign");

        var rejected = await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "reject", "Endettement trop élevé.");

        rejected.Status.ShouldBe(ApplicationStatus.Rejected);
        rejected.DecisionComment.ShouldBe("Endettement trop élevé.");
        rejected.Events[^1].FromStatus.ShouldBe(ApplicationStatus.UnderReview);
    }

    [Fact]
    public async Task Client_detail_hides_the_internal_score()
    {
        var client = await _api.RegisterClientAsync();
        var submitted = await ApiDriver.SubmittedApplicationAsync(client);

        submitted.RiskScore.ShouldBeNull();
        submitted.RiskGrade.ShouldBeNull();
        submitted.Recommendation.ShouldBeNull();
        submitted.MonthlyPayment.ShouldNotBeNull();
    }

    [Fact]
    public async Task Forbidden_transitions_return_409()
    {
        var client = await _api.RegisterClientAsync();
        var analyst = await _api.StaffAsync(RoleNames.Analyst);
        var submitted = await ApiDriver.SubmittedApplicationAsync(client);

        // Décider sans assignation, soumettre deux fois.
        var approve = await analyst.Http.PostAsJsonAsync($"/api/v1/review/applications/{submitted.Id}/approve", new { comment = "OK" });
        (await ApiDriver.ProblemAsync(approve, HttpStatusCode.Conflict)).GetProperty("code").GetString().ShouldBe("invalid_transition");
        await ApiDriver.ProblemAsync(await client.Http.PostAsync($"/api/v1/applications/{submitted.Id}/submit", null), HttpStatusCode.Conflict);

        // Assigner deux fois, annuler en cours d'étude, modifier une demande soumise.
        await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "assign");
        await ApiDriver.ProblemAsync(await analyst.Http.PostAsync($"/api/v1/review/applications/{submitted.Id}/assign", null), HttpStatusCode.Conflict);
        await ApiDriver.ProblemAsync(await client.Http.PostAsync($"/api/v1/applications/{submitted.Id}/cancel", null), HttpStatusCode.Conflict);
        var edit = await client.Http.PutAsJsonAsync($"/api/v1/applications/{submitted.Id}", new { productType = "CONSOMMATION", amount = 10_000, durationMonths = 12 });
        (await ApiDriver.ProblemAsync(edit, HttpStatusCode.Conflict)).GetProperty("code").GetString().ShouldBe("not_editable");

        // Une décision finale est définitive.
        await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "approve", "Accord.");
        await ApiDriver.ProblemAsync(await analyst.Http.PostAsJsonAsync($"/api/v1/review/applications/{submitted.Id}/reject", new { comment = "Non" }), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Client_can_cancel_a_submitted_application()
    {
        var client = await _api.RegisterClientAsync();
        var submitted = await ApiDriver.SubmittedApplicationAsync(client);

        var response = await client.Http.PostAsJsonAsync($"/api/v1/applications/{submitted.Id}/cancel", new { comment = "Projet reporté" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cancelled = await ApiDriver.ReadAsync<ApplicationDetailDto>(response);
        cancelled.Status.ShouldBe(ApplicationStatus.Cancelled);
        cancelled.Events[^1].Comment.ShouldBe("Projet reporté");
    }

    [Fact]
    public async Task Stale_if_match_version_is_a_conflict()
    {
        var client = await _api.RegisterClientAsync();
        var draft = await ApiDriver.CreateDraftAsync(client);

        using var first = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/applications/{draft.Id}")
        {
            Content = JsonContent.Create(new { productType = "CONSOMMATION", amount = 60_000, durationMonths = 36 }),
        };
        first.Headers.TryAddWithoutValidation("If-Match", $"\"{draft.Version}\"");
        var ok = await client.Http.SendAsync(first);
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        ok.Headers.ETag!.Tag.ShouldNotBe($"\"{draft.Version}\"");

        using var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/applications/{draft.Id}")
        {
            Content = JsonContent.Create(new { productType = "CONSOMMATION", amount = 70_000, durationMonths = 36 }),
        };
        stale.Headers.TryAddWithoutValidation("If-Match", $"\"{draft.Version}\"");
        var conflict = await client.Http.SendAsync(stale);

        (await ApiDriver.ProblemAsync(conflict, HttpStatusCode.Conflict)).GetProperty("code").GetString().ShouldBe("concurrency_conflict");
    }

    [Fact]
    public async Task Simultaneous_decisions_only_one_wins()
    {
        var client = await _api.RegisterClientAsync();
        var analyst = await _api.StaffAsync(RoleNames.Analyst);
        var admin = await _api.StaffAsync(RoleNames.Admin);
        var submitted = await ApiDriver.SubmittedApplicationAsync(client);
        await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "assign");

        var responses = await Task.WhenAll(
            analyst.Http.PostAsJsonAsync($"/api/v1/review/applications/{submitted.Id}/approve", new { comment = "Accord analyste" }),
            admin.Http.PostAsJsonAsync($"/api/v1/review/applications/{submitted.Id}/reject", new { comment = "Refus admin" }));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        var detail = await client.Http.GetFromJsonAsync<ApplicationDetailDto>($"/api/v1/applications/{submitted.Id}", ApiDriver.Json);
        detail!.Events.Count(e => e.ToStatus is ApplicationStatus.Approved or ApplicationStatus.Rejected).ShouldBe(1);
    }

    [Fact]
    public async Task Concurrent_modification_through_the_database_is_detected()
    {
        var client = await _api.RegisterClientAsync();
        var draft = await ApiDriver.CreateDraftAsync(client);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            client.Http.PutAsJsonAsync($"/api/v1/applications/{draft.Id}", new { productType = "CONSOMMATION", amount = 20_000 + (i * 1000), durationMonths = 24 })));

        results.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict);
        results.ShouldContain(r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Analyst_not_assigned_cannot_decide()
    {
        var client = await _api.RegisterClientAsync();
        var analyst = await _api.StaffAsync(RoleNames.Analyst);
        var otherAnalyst = await _api.StaffAsync(RoleNames.Analyst);
        var submitted = await ApiDriver.SubmittedApplicationAsync(client);
        await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "assign");

        var response = await otherAnalyst.Http.PostAsJsonAsync($"/api/v1/review/applications/{submitted.Id}/approve", new { comment = "Je valide" });

        await ApiDriver.ProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Decision_without_comment_is_a_validation_error()
    {
        var client = await _api.RegisterClientAsync();
        var analyst = await _api.StaffAsync(RoleNames.Analyst);
        var submitted = await ApiDriver.SubmittedApplicationAsync(client);
        await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "assign");

        var response = await analyst.Http.PostAsJsonAsync($"/api/v1/review/applications/{submitted.Id}/reject", new { comment = "  " });

        (await ApiDriver.ProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("comment", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Client_list_is_paginated_and_filtered_by_status()
    {
        var client = await _api.RegisterClientAsync();
        for (var i = 0; i < 3; i++)
        {
            await ApiDriver.CreateDraftAsync(client, amount: 10_000m + i);
        }

        var page1 = await ApiDriver.PageAsync<ApplicationSummaryDto>(client.Http, "/api/v1/applications/mine?page=1&pageSize=2");
        page1.Items.Count.ShouldBe(2);
        page1.TotalCount.ShouldBe(3);
        page1.TotalPages.ShouldBe(2);
        var page2 = await ApiDriver.PageAsync<ApplicationSummaryDto>(client.Http, "/api/v1/applications/mine?page=2&pageSize=2");
        page2.Items.Count.ShouldBe(1);
        page1.Items.Concat(page2.Items).Select(a => a.Id).Distinct().Count().ShouldBe(3);

        var drafts = await ApiDriver.PageAsync<ApplicationSummaryDto>(client.Http, "/api/v1/applications/mine?status=DRAFT");
        drafts.TotalCount.ShouldBe(3);
        (await ApiDriver.PageAsync<ApplicationSummaryDto>(client.Http, "/api/v1/applications/mine?status=SUBMITTED")).TotalCount.ShouldBe(0);

        await ApiDriver.ProblemAsync(await client.Http.GetAsync("/api/v1/applications/mine?pageSize=101"), HttpStatusCode.BadRequest);
        await ApiDriver.ProblemAsync(await client.Http.GetAsync("/api/v1/applications/mine?status=UNKNOWN"), HttpStatusCode.BadRequest);
    }
}
