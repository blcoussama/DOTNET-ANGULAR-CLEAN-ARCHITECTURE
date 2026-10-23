using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CreditFlow.Api.IntegrationTests.Infrastructure;
using CreditFlow.Application.Products;
using CreditFlow.Application.Profiles;
using CreditFlow.Application.Simulations;
using CreditFlow.Domain.Clients;

namespace CreditFlow.Api.IntegrationTests;

[Collection(SharedApiDefinition.Name)]
public sealed class PublicEndpointTests(CreditFlowApiFactory factory)
{
    private readonly ApiDriver _api = new(factory);

    [Fact]
    public async Task Health_is_public_and_checks_the_database()
    {
        using var http = _api.Anonymous();

        var response = await http.GetAsync("/api/v1/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("status").GetString().ShouldBe("Healthy");
        body.GetProperty("checks").GetProperty("database").GetString().ShouldBe("Healthy");
    }

    [Fact]
    public async Task Active_products_are_public_with_string_enums()
    {
        using var http = _api.Anonymous();

        var json = await http.GetStringAsync("/api/v1/products");
        var products = JsonSerializer.Deserialize<List<ProductDto>>(json, ApiDriver.Json)!;

        products.Select(p => p.Type.ToString()).ShouldBe(["Consommation", "Auto", "Immobilier"]);
        json.ShouldContain("\"type\":\"CONSOMMATION\"");
        json.ShouldContain("\"annualRate\":");
    }

    [Fact]
    public async Task Simulation_is_public_and_follows_the_annuity_formula()
    {
        using var http = _api.Anonymous();

        var response = await http.PostAsJsonAsync("/api/v1/simulations", new { productType = "CONSOMMATION", amount = 100_000, durationMonths = 60 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await ApiDriver.ReadAsync<SimulationResult>(response);
        result.AnnualRate.ShouldBe(7.5m);
        result.MonthlyPayment.ShouldBe(2003.79m);
        result.TotalCost.ShouldBe(20227.40m);
        result.DebtRatio.ShouldBeNull();
        result.WithinDebtThreshold.ShouldBeNull();
    }

    [Fact]
    public async Task Simulation_with_income_reports_the_debt_ratio()
    {
        using var http = _api.Anonymous();

        var response = await http.PostAsJsonAsync("/api/v1/simulations", new { productType = "CONSOMMATION", amount = 100_000, durationMonths = 60, monthlyIncome = 5_000, monthlyCharges = 0 });

        var result = await ApiDriver.ReadAsync<SimulationResult>(response);
        result.DebtRatio.ShouldBe(0.4008m);
        result.WithinDebtThreshold.ShouldBe(false);
        result.DebtThreshold.ShouldBe(0.40m);
    }

    [Fact]
    public async Task Simulation_validates_bounds_and_enums()
    {
        using var http = _api.Anonymous();

        var outOfBounds = await http.PostAsJsonAsync("/api/v1/simulations", new { productType = "IMMOBILIER", amount = 10_000, durationMonths = 400 });
        var errors = (await ApiDriver.ProblemAsync(outOfBounds, HttpStatusCode.BadRequest)).GetProperty("errors");
        errors.TryGetProperty("amount", out _).ShouldBeTrue();
        errors.TryGetProperty("durationMonths", out _).ShouldBeTrue();

        var badEnum = await http.PostAsJsonAsync("/api/v1/simulations", new { productType = "LEASING", amount = 10_000, durationMonths = 12 });
        (await ApiDriver.ProblemAsync(badEnum, HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("productType", out _).ShouldBeTrue();

        var numericEnum = await http.PostAsJsonAsync("/api/v1/simulations", new { productType = 0, amount = 10_000, durationMonths = 12 });
        await ApiDriver.ProblemAsync(numericEnum, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Malformed_json_returns_a_validation_problem()
    {
        using var http = _api.Anonymous();
        using var content = new StringContent("{ \"productType\": ", Encoding.UTF8, "application/json");

        var response = await http.PostAsync("/api/v1/simulations", content);

        var problem = await ApiDriver.ProblemAsync(response, HttpStatusCode.BadRequest);
        problem.GetProperty("title").GetString().ShouldBe("Données invalides");
        problem.TryGetProperty("errors", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Unknown_routes_return_problem_details_and_openapi_is_hidden_outside_development()
    {
        var client = await _api.RegisterClientAsync();

        var problem = await ApiDriver.ProblemAsync(await client.Http.GetAsync("/api/v1/does-not-exist"), HttpStatusCode.NotFound);
        problem.GetProperty("title").GetString().ShouldBe("Ressource introuvable");
        (await client.Http.GetAsync("/openapi/v1.json")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.Http.GetAsync("/scalar")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        using var http = _api.Anonymous();

        var response = await http.GetAsync("/api/v1/products");

        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        response.Headers.Contains("Content-Security-Policy").ShouldBeTrue();
        response.Headers.Contains("Server").ShouldBeFalse();
    }
}

[Collection(SharedApiDefinition.Name)]
public sealed class ClientProfileTests(CreditFlowApiFactory factory)
{
    private readonly ApiDriver _api = new(factory);

    [Fact]
    public async Task Profile_is_not_found_until_first_saved()
    {
        var client = await _api.RegisterClientAsync();

        await ApiDriver.ProblemAsync(await client.Http.GetAsync("/api/v1/clients/me/profile"), HttpStatusCode.NotFound);

        var partial = await client.Http.PutAsJsonAsync("/api/v1/clients/me/profile", new { city = "Fès" });
        (await ApiDriver.ReadAsync<ClientProfileDto>(partial)).IsComplete.ShouldBeFalse();
        var profile = await client.Http.GetFromJsonAsync<ClientProfileDto>("/api/v1/clients/me/profile", ApiDriver.Json);
        profile!.City.ShouldBe("Fès");
        profile.IsComplete.ShouldBeFalse();
    }

    [Fact]
    public async Task Complete_profile_is_reported_as_complete()
    {
        var client = await _api.RegisterClientAsync();

        var response = await client.Http.PutAsJsonAsync("/api/v1/clients/me/profile", new
        {
            dateOfBirth = "1988-02-29",
            phone = "0661000000",
            city = "Casablanca",
            employmentType = "SANS_EMPLOI",
            monthlyIncome = 4_000.50,
            monthlyCharges = 0,
            yearsAtCurrentJob = 0,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.ShouldContain("\"employmentType\":\"SANS_EMPLOI\"");
        json.ShouldContain("\"dateOfBirth\":\"1988-02-29\"");
        var profile = JsonSerializer.Deserialize<ClientProfileDto>(json, ApiDriver.Json)!;
        profile.IsComplete.ShouldBeTrue();
        profile.EmploymentType.ShouldBe(EmploymentType.SansEmploi);
        profile.MonthlyIncome.ShouldBe(4_000.50m);
    }

    [Fact]
    public async Task Invalid_profile_values_are_reported_per_field()
    {
        var client = await _api.RegisterClientAsync();

        var response = await client.Http.PutAsJsonAsync("/api/v1/clients/me/profile", new { dateOfBirth = "2015-01-01", monthlyIncome = 0, monthlyCharges = -10, phone = "abc" });

        var errors = (await ApiDriver.ProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        errors.EnumerateObject().Select(p => p.Name).ShouldBe(["dateOfBirth", "phone", "monthlyIncome", "monthlyCharges"], ignoreOrder: true);
    }
}
