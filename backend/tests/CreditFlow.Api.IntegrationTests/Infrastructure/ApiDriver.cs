using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CreditFlow.Application.Applications;
using CreditFlow.Application.Auth;
using CreditFlow.Application.Common;
using CreditFlow.Application.Review;

namespace CreditFlow.Api.IntegrationTests.Infrastructure;

/// <summary>Utilisateur connecté : client HTTP porteur de son jeton d'accès + refresh token du cookie.</summary>
public sealed record TestSession(HttpClient Http, Guid UserId, string Email, string Role, string AccessToken, string RefreshToken);

/// <summary>Raccourcis de scénario au-dessus de l'API HTTP réelle.</summary>
public sealed class ApiDriver(CreditFlowApiFactory factory)
{
    public static readonly JsonSerializerOptions Json = CreateJsonOptions();

    public static readonly byte[] PdfBytes = [.. "%PDF-1.4\n% CreditFlow test\n"u8, .. new byte[256], .. "\n%%EOF"u8];

    public static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];

    public CreditFlowApiFactory Factory => factory;

    public HttpClient Anonymous() => factory.CreateApiClient();

    public async Task<TestSession> RegisterClientAsync(string firstName = "Salma", string lastName = "Alaoui")
    {
        var email = $"client-{Guid.NewGuid():N}@test.local";
        using var anonymous = Anonymous();
        var response = await anonymous.PostAsJsonAsync("/api/v1/auth/register", new { email, password = CreditFlowApiFactory.Password, firstName, lastName }, Json);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await LoginAsync(email);
    }

    public async Task<TestSession> StaffAsync(string role)
    {
        var (_, email) = await factory.CreateUserAsync(role);
        return await LoginAsync(email);
    }

    public async Task<TestSession> LoginAsync(string email, string password = CreditFlowApiFactory.Password)
    {
        using var anonymous = Anonymous();
        var response = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Json);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        var http = factory.CreateApiClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new TestSession(http, auth.User.Id, email, auth.User.Role, auth.AccessToken, RefreshCookie(response)!);
    }

    public static string? RefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Where(c => c.StartsWith("refreshToken=", StringComparison.Ordinal))
                .Select(c => c.Split(';')[0]["refreshToken=".Length..])
                .FirstOrDefault()
            : null;

    public async Task<HttpResponseMessage> RefreshAsync(string? refreshToken)
    {
        using var anonymous = Anonymous();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        if (refreshToken is not null)
        {
            request.Headers.Add("Cookie", $"refreshToken={refreshToken}");
        }

        return await anonymous.SendAsync(request);
    }

    public static async Task CompleteProfileAsync(TestSession client, decimal monthlyIncome = 10_000m, decimal monthlyCharges = 1_000m, string employmentType = "CDI", int years = 6)
    {
        var response = await client.Http.PutAsJsonAsync("/api/v1/clients/me/profile", new
        {
            dateOfBirth = "1990-05-12",
            phone = "+212 600 000 000",
            city = "Tanger",
            employmentType,
            employer = "ACME SARL",
            monthlyIncome,
            monthlyCharges,
            yearsAtCurrentJob = years,
        }, Json);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    public static async Task<ApplicationDetailDto> CreateDraftAsync(TestSession client, string productType = "CONSOMMATION", decimal amount = 100_000m, int durationMonths = 60)
    {
        var response = await client.Http.PostAsJsonAsync("/api/v1/applications", new { productType, amount, durationMonths, purpose = "Travaux de rénovation" }, Json);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailDto>(Json))!;
    }

    public static async Task<HttpResponseMessage> UploadAsync(HttpClient http, Guid applicationId, string? type, byte[] content, string fileName = "piece.pdf", string contentType = "application/pdf")
    {
        using var form = new MultipartFormDataContent();
        if (type is not null)
        {
            form.Add(new StringContent(type), "type");
        }

        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return await http.PostAsync($"/api/v1/applications/{applicationId}/documents", form);
    }

    public static async Task<DocumentDto> UploadOkAsync(TestSession client, Guid applicationId, string type)
    {
        var response = await UploadAsync(client.Http, applicationId, type, PdfBytes, $"{type.ToLowerInvariant()}.pdf");
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<DocumentDto>(Json))!;
    }

    /// <summary>Profil complet + brouillon + pièces obligatoires + soumission.</summary>
    public static async Task<ApplicationDetailDto> SubmittedApplicationAsync(TestSession client, string productType = "CONSOMMATION", decimal amount = 100_000m, int durationMonths = 60)
    {
        await CompleteProfileAsync(client);
        var draft = await CreateDraftAsync(client, productType, amount, durationMonths);
        await UploadOkAsync(client, draft.Id, "ID_CARD");
        await UploadOkAsync(client, draft.Id, "PAYSLIP");
        var response = await client.Http.PostAsync($"/api/v1/applications/{draft.Id}/submit", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailDto>(Json))!;
    }

    public static async Task<ReviewApplicationDetailDto> ReviewActionAsync(TestSession reviewer, Guid applicationId, string action, string? comment = "Commentaire de l'analyste", HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = action == "assign"
            ? await reviewer.Http.PostAsync($"/api/v1/review/applications/{applicationId}/assign", null)
            : await reviewer.Http.PostAsJsonAsync($"/api/v1/review/applications/{applicationId}/{action}", new { comment }, Json);
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
        return expected == HttpStatusCode.OK ? (await response.Content.ReadFromJsonAsync<ReviewApplicationDetailDto>(Json))! : null!;
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static async Task<JsonElement> ProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, body);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = JsonDocument.Parse(body).RootElement.Clone();
        problem.GetProperty("status").GetInt32().ShouldBe((int)expected);
        problem.TryGetProperty("title", out _).ShouldBeTrue();
        problem.TryGetProperty("traceId", out _).ShouldBeTrue();
        return problem;
    }

    public static async Task<PagedResult<T>> PageAsync<T>(HttpClient http, string url)
    {
        var response = await http.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PagedResult<T>>(Json))!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(EnumWire.NamingPolicy, allowIntegerValues: false));
        return options;
    }
}
