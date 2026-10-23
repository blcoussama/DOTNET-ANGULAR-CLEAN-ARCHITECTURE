using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CreditFlow.Api.IntegrationTests.Infrastructure;
using CreditFlow.Application.Products;
using CreditFlow.Application.Review;
using CreditFlow.Application.Simulations;
using CreditFlow.Application.Users;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Api.IntegrationTests;

[Collection(SharedApiDefinition.Name)]
public sealed class ReviewQueueTests(CreditFlowApiFactory factory)
{
    private readonly ApiDriver _api = new(factory);

    /// <summary>Trois demandes soumises d'un même client au nom unique (A : 100 000 et 20 000 en consommation, C : 250 000 en auto).</summary>
    private async Task<(string LastName, TestSession Analyst, Guid[] Ids)> SeedAsync()
    {
        var lastName = "File" + Guid.NewGuid().ToString("N")[..8];
        var client = await _api.RegisterClientAsync("Imane", lastName);
        var analyst = await _api.StaffAsync(RoleNames.Analyst);
        var a = await ApiDriver.SubmittedApplicationAsync(client, "CONSOMMATION", 100_000m, 60);
        var b = await ApiDriver.SubmittedApplicationAsync(client, "AUTO", 250_000m, 84);
        var c = await ApiDriver.SubmittedApplicationAsync(client, "CONSOMMATION", 20_000m, 24);
        return (lastName, analyst, [a.Id, b.Id, c.Id]);
    }

    [Fact]
    public async Task Queue_is_paginated_searchable_and_sortable()
    {
        var (lastName, analyst, _) = await SeedAsync();

        var all = await ApiDriver.PageAsync<ReviewQueueItemDto>(analyst.Http, $"/api/v1/review/applications?search={lastName.ToLowerInvariant()}&sortBy=amount&sortDir=desc");
        all.TotalCount.ShouldBe(3);
        all.Items.Select(i => i.Amount).ShouldBe([250_000m, 100_000m, 20_000m]);
        all.Items.ShouldAllBe(i => i.ClientName == $"Imane {lastName}" && i.Status == ApplicationStatus.Submitted && i.RiskScore != null);

        var page = await ApiDriver.PageAsync<ReviewQueueItemDto>(analyst.Http, $"/api/v1/review/applications?search={lastName}&page=2&pageSize=2&sortBy=submittedAt&sortDir=asc");
        page.Items.Count.ShouldBe(1);
        page.TotalPages.ShouldBe(2);
        page.Items[0].Amount.ShouldBe(20_000m);

        var byScore = await ApiDriver.PageAsync<ReviewQueueItemDto>(analyst.Http, $"/api/v1/review/applications?search={lastName}&sortBy=riskScore&sortDir=asc");
        byScore.Items[0].RiskGrade.ShouldBe(RiskGrade.C);
    }

    [Fact]
    public async Task Queue_filters_by_grade_product_status_and_assignment()
    {
        var (lastName, analyst, ids) = await SeedAsync();
        await ApiDriver.ReviewActionAsync(analyst, ids[0], "assign");

        (await ApiDriver.PageAsync<ReviewQueueItemDto>(analyst.Http, $"/api/v1/review/applications?search={lastName}&riskGrade=C")).Items.Single().Id.ShouldBe(ids[1]);
        (await ApiDriver.PageAsync<ReviewQueueItemDto>(analyst.Http, $"/api/v1/review/applications?search={lastName}&productType=AUTO")).Items.Single().Id.ShouldBe(ids[1]);
        (await ApiDriver.PageAsync<ReviewQueueItemDto>(analyst.Http, $"/api/v1/review/applications?search={lastName}&status=UNDER_REVIEW")).Items.Single().Id.ShouldBe(ids[0]);
        var mine = await ApiDriver.PageAsync<ReviewQueueItemDto>(analyst.Http, "/api/v1/review/applications?assignedToMe=true");
        mine.Items.Single().Id.ShouldBe(ids[0]);
        mine.Items[0].AssignedAnalystName.ShouldBe("Analyst Test");
    }

    [Fact]
    public async Task Queue_search_matches_the_reference()
    {
        var client = await _api.RegisterClientAsync();
        var analyst = await _api.StaffAsync(RoleNames.Analyst);
        var submitted = await ApiDriver.SubmittedApplicationAsync(client);

        var result = await ApiDriver.PageAsync<ReviewQueueItemDto>(analyst.Http, $"/api/v1/review/applications?search={submitted.Reference}");

        result.Items.Single().Id.ShouldBe(submitted.Id);
    }

    [Fact]
    public async Task Queue_rejects_invalid_parameters()
    {
        var analyst = await _api.StaffAsync(RoleNames.Analyst);

        var problem = await ApiDriver.ProblemAsync(await analyst.Http.GetAsync("/api/v1/review/applications?sortBy=name&riskGrade=Z&pageSize=500"), HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("sortBy", out _).ShouldBeTrue();
        errors.TryGetProperty("riskGrade", out _).ShouldBeTrue();
        await ApiDriver.ProblemAsync(await analyst.Http.GetAsync("/api/v1/review/applications?status=DRAFT"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Review_detail_exposes_profile_and_score_breakdown()
    {
        var client = await _api.RegisterClientAsync("Omar", "Detail");
        var analyst = await _api.StaffAsync(RoleNames.Analyst);
        var submitted = await ApiDriver.SubmittedApplicationAsync(client, "AUTO", 250_000m, 84);

        var detail = await analyst.Http.GetFromJsonAsync<ReviewApplicationDetailDto>($"/api/v1/review/applications/{submitted.Id}", ApiDriver.Json);

        detail!.Client!.LastName.ShouldBe("Detail");
        detail.Client.Email.ShouldEndWith("@test.local");
        detail.ClientProfile.ShouldNotBeNull().MonthlyIncome.ShouldBe(10_000m);
        var breakdown = detail.ScoringBreakdown.ShouldNotBeNull();
        breakdown.Total.ShouldBe(59);
        (breakdown.DebtRatioPoints, breakdown.EmploymentPoints, breakdown.SeniorityPoints, breakdown.AmountToIncomePoints).ShouldBe((10, 22, 15, 12));
        breakdown.Grade.ShouldBe(RiskGrade.C);
        breakdown.Recommendation.ShouldBe(Recommendation.Defavorable);
        breakdown.DebtRatio.ShouldBe(0.4712m);
        breakdown.WithinDebtThreshold.ShouldBeFalse();
        breakdown.Criteria.Select(c => (c.Criterion, c.Points)).ShouldBe(
            [("DEBT_RATIO", 10), ("EMPLOYMENT_STABILITY", 22), ("SENIORITY", 15), ("AMOUNT_TO_INCOME", 12)]);
        detail.Documents.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Dashboard_returns_consistent_indicators()
    {
        var client = await _api.RegisterClientAsync();
        var analyst = await _api.StaffAsync(RoleNames.Analyst);
        var submitted = await ApiDriver.SubmittedApplicationAsync(client, "IMMOBILIER", 1_000_000m, 240);
        await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "assign");
        await ApiDriver.ReviewActionAsync(analyst, submitted.Id, "approve", "Accord");

        var response = await analyst.Http.GetAsync("/api/v1/dashboard/stats");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stats = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var counts = stats.GetProperty("countsByStatus");
        counts.EnumerateObject().Select(p => p.Name).ShouldBe(["DRAFT", "SUBMITTED", "UNDER_REVIEW", "INFO_REQUESTED", "APPROVED", "REJECTED", "CANCELLED"]);
        counts.GetProperty("APPROVED").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        stats.GetProperty("totalApprovedAmount").GetDecimal().ShouldBeGreaterThanOrEqualTo(1_000_000m);
        stats.GetProperty("approvalRate").GetDecimal().ShouldBeInRange(0m, 1m);
        stats.GetProperty("monthlyTrend").GetArrayLength().ShouldBe(6);
        stats.GetProperty("monthlyTrend")[5].GetProperty("approved").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        stats.GetProperty("volumeByProduct").GetArrayLength().ShouldBe(3);
        stats.GetProperty("gradeDistribution").EnumerateObject().Select(p => p.Name).ShouldBe(["A", "B", "C", "D", "E"]);
        stats.TryGetProperty("averageProcessingDays", out _).ShouldBeTrue();
        stats.TryGetProperty("averageAmount", out _).ShouldBeTrue();
    }
}

[Collection(SharedApiDefinition.Name)]
public sealed class AdminTests(CreditFlowApiFactory factory)
{
    private readonly ApiDriver _api = new(factory);

    [Fact]
    public async Task Admin_creates_an_analyst_who_can_then_log_in()
    {
        var admin = await _api.StaffAsync(RoleNames.Admin);
        var email = $"analyste-{Guid.NewGuid():N}@test.local";

        var response = await admin.Http.PostAsJsonAsync("/api/v1/admin/users", new { email, firstName = "Hind", lastName = "Amrani", role = "Analyst", temporaryPassword = "Temporaire1!" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ApiDriver.ReadAsync<AdminUserDto>(response);
        created.Role.ShouldBe("Analyst");
        created.IsActive.ShouldBeTrue();
        var session = await _api.LoginAsync(email, "Temporaire1!");
        session.Role.ShouldBe("Analyst");
        (await session.Http.GetAsync("/api/v1/review/applications")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await ApiDriver.ProblemAsync(await admin.Http.PostAsJsonAsync("/api/v1/admin/users", new { email, firstName = "X", lastName = "Y", role = "Analyst", temporaryPassword = "Temporaire1!" }), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Admin_cannot_create_client_accounts_or_weak_passwords()
    {
        var admin = await _api.StaffAsync(RoleNames.Admin);

        var response = await admin.Http.PostAsJsonAsync("/api/v1/admin/users", new { email = "x@test.local", firstName = "X", lastName = "Y", role = "Client", temporaryPassword = "faible" });

        var errors = (await ApiDriver.ProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        errors.TryGetProperty("role", out _).ShouldBeTrue();
        errors.TryGetProperty("temporaryPassword", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Admin_cannot_disable_himself_nor_drop_his_own_role()
    {
        var admin = await _api.StaffAsync(RoleNames.Admin);

        var disable = await admin.Http.PatchAsJsonAsync($"/api/v1/admin/users/{admin.UserId}", new { isActive = false });
        (await ApiDriver.ProblemAsync(disable, HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("isActive", out _).ShouldBeTrue();
        var demote = await admin.Http.PatchAsJsonAsync($"/api/v1/admin/users/{admin.UserId}", new { role = "Analyst" });
        (await ApiDriver.ProblemAsync(demote, HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("role", out _).ShouldBeTrue();
        await ApiDriver.ProblemAsync(await admin.Http.PatchAsJsonAsync($"/api/v1/admin/users/{Guid.NewGuid()}", new { isActive = false }), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Role_change_invalidates_existing_tokens()
    {
        var admin = await _api.StaffAsync(RoleNames.Admin);
        var analyst = await _api.StaffAsync(RoleNames.Analyst);

        var response = await admin.Http.PatchAsJsonAsync($"/api/v1/admin/users/{analyst.UserId}", new { role = "Admin" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiDriver.ReadAsync<AdminUserDto>(response)).Role.ShouldBe("Admin");
        (await analyst.Http.GetAsync("/api/v1/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _api.RefreshAsync(analyst.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _api.LoginAsync(analyst.Email)).Role.ShouldBe("Admin");
    }

    [Fact]
    public async Task Client_accounts_cannot_be_turned_into_staff()
    {
        var admin = await _api.StaffAsync(RoleNames.Admin);
        var client = await _api.RegisterClientAsync();

        var toAnalyst = await admin.Http.PatchAsJsonAsync($"/api/v1/admin/users/{client.UserId}", new { role = "Analyst" });
        (await ApiDriver.ProblemAsync(toAnalyst, HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("role", out _).ShouldBeTrue();
        var analyst = await _api.StaffAsync(RoleNames.Analyst);
        await ApiDriver.ProblemAsync(await admin.Http.PatchAsJsonAsync($"/api/v1/admin/users/{analyst.UserId}", new { role = "Client" }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Users_can_be_listed_with_role_and_search_filters()
    {
        var admin = await _api.StaffAsync(RoleNames.Admin);
        var client = await _api.RegisterClientAsync("Lina", "Recherche" + Guid.NewGuid().ToString("N")[..6]);

        var byEmail = await ApiDriver.PageAsync<AdminUserDto>(admin.Http, $"/api/v1/admin/users?search={client.Email[..20]}");
        byEmail.Items.Single().Id.ShouldBe(client.UserId);
        byEmail.Items[0].Role.ShouldBe("Client");

        var analysts = await ApiDriver.PageAsync<AdminUserDto>(admin.Http, "/api/v1/admin/users?role=Analyst&pageSize=100");
        analysts.Items.ShouldAllBe(u => u.Role == "Analyst");
        (await ApiDriver.PageAsync<AdminUserDto>(admin.Http, $"/api/v1/admin/users?role=Admin&search={admin.Email}")).Items.Single().Id.ShouldBe(admin.UserId);
        await ApiDriver.ProblemAsync(await admin.Http.GetAsync("/api/v1/admin/users?role=Root"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_updates_a_product_and_simulations_follow()
    {
        var admin = await _api.StaffAsync(RoleNames.Admin);
        var original = (await admin.Http.GetFromJsonAsync<List<ProductDto>>("/api/v1/admin/products", ApiDriver.Json))!.Single(p => p.Type == ProductType.Auto);
        try
        {
            var update = await admin.Http.PutAsJsonAsync("/api/v1/admin/products/AUTO", new { annualRate = 0, minAmount = 10_000, maxAmount = 700_000, minDurationMonths = 12, maxDurationMonths = 96, isActive = true });
            update.StatusCode.ShouldBe(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
            (await ApiDriver.ReadAsync<ProductDto>(update)).MaxDurationMonths.ShouldBe(96);

            using var anonymous = _api.Anonymous();
            var simulation = await anonymous.PostAsJsonAsync("/api/v1/simulations", new { productType = "AUTO", amount = 12_000, durationMonths = 96 });
            simulation.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await ApiDriver.ReadAsync<SimulationResult>(simulation)).MonthlyPayment.ShouldBe(125.00m);

            var deactivate = await admin.Http.PutAsJsonAsync("/api/v1/admin/products/auto", new { annualRate = 6.5, minAmount = 20_000, maxAmount = 600_000, minDurationMonths = 12, maxDurationMonths = 84, isActive = false });
            deactivate.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await anonymous.GetFromJsonAsync<List<ProductDto>>("/api/v1/products", ApiDriver.Json))!.ShouldNotContain(p => p.Type == ProductType.Auto);
            (await admin.Http.GetFromJsonAsync<List<ProductDto>>("/api/v1/admin/products", ApiDriver.Json))!.Count.ShouldBe(3);
        }
        finally
        {
            var restore = await admin.Http.PutAsJsonAsync("/api/v1/admin/products/AUTO", new
            {
                annualRate = original.AnnualRate,
                minAmount = original.MinAmount,
                maxAmount = original.MaxAmount,
                minDurationMonths = original.MinDurationMonths,
                maxDurationMonths = original.MaxDurationMonths,
                isActive = true,
            });
            restore.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Product_update_is_validated()
    {
        var admin = await _api.StaffAsync(RoleNames.Admin);

        var incoherent = await admin.Http.PutAsJsonAsync("/api/v1/admin/products/AUTO", new { annualRate = 45, minAmount = 50_000, maxAmount = 10_000, minDurationMonths = 12, maxDurationMonths = 84, isActive = true });
        var errors = (await ApiDriver.ProblemAsync(incoherent, HttpStatusCode.BadRequest)).GetProperty("errors");
        errors.TryGetProperty("annualRate", out _).ShouldBeTrue();
        errors.TryGetProperty("maxAmount", out _).ShouldBeTrue();
        await ApiDriver.ProblemAsync(await admin.Http.PutAsJsonAsync("/api/v1/admin/products/LEASING", new { annualRate = 5, minAmount = 1, maxAmount = 2, minDurationMonths = 1, maxDurationMonths = 2, isActive = true }), HttpStatusCode.BadRequest);
    }
}
