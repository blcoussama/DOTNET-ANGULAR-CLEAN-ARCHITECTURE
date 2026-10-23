using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CreditFlow.Api.IntegrationTests.Infrastructure;
using CreditFlow.Application.Auth;

namespace CreditFlow.Api.IntegrationTests;

[Collection(SharedApiDefinition.Name)]
public sealed class AuthTests(CreditFlowApiFactory factory)
{
    private readonly ApiDriver _api = new(factory);

    [Fact]
    public async Task Register_creates_a_client_account()
    {
        using var http = _api.Anonymous();
        var email = $"new-{Guid.NewGuid():N}@test.local";

        var response = await http.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "Motdepasse1!", firstName = "Nora", lastName = "Bennani" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var user = await ApiDriver.ReadAsync<UserDto>(response);
        user.Email.ShouldBe(email);
        user.Role.ShouldBe("Client");
        user.FirstName.ShouldBe("Nora");
    }

    [Fact]
    public async Task Register_with_an_existing_email_is_a_conflict()
    {
        var client = await _api.RegisterClientAsync();
        using var http = _api.Anonymous();

        var response = await http.PostAsJsonAsync("/api/v1/auth/register", new { email = client.Email, password = "Motdepasse1!", firstName = "A", lastName = "B" });

        var problem = await ApiDriver.ProblemAsync(response, HttpStatusCode.Conflict);
        problem.GetProperty("code").GetString().ShouldBe("email_taken");
    }

    [Fact]
    public async Task Register_with_a_weak_password_returns_field_errors()
    {
        using var http = _api.Anonymous();

        var response = await http.PostAsJsonAsync("/api/v1/auth/register", new { email = "pas-un-email", password = "Motdepasse1", firstName = "", lastName = "B" });

        var problem = await ApiDriver.ProblemAsync(response, HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("password", out _).ShouldBeTrue();
        errors.TryGetProperty("email", out _).ShouldBeTrue();
        errors.TryGetProperty("firstName", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Login_returns_an_access_token_and_a_hardened_refresh_cookie()
    {
        var client = await _api.RegisterClientAsync();
        using var http = _api.Anonymous();

        var response = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = client.Email, password = CreditFlowApiFactory.Password });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = await ApiDriver.ReadAsync<AuthResponse>(response);
        auth.ExpiresIn.ShouldBe(900);
        auth.AccessToken.Split('.').Length.ShouldBe(3);
        auth.User.Role.ShouldBe("Client");
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("refreshToken=", StringComparison.Ordinal)).ToLowerInvariant();
        cookie.ShouldContain("httponly");
        cookie.ShouldContain("secure");
        cookie.ShouldContain("samesite=strict");
        cookie.ShouldContain("path=/api/v1/auth");
    }

    [Fact]
    public async Task Login_with_a_wrong_password_is_unauthorized()
    {
        var client = await _api.RegisterClientAsync();
        using var http = _api.Anonymous();

        var response = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = client.Email, password = "Mauvais1234" });

        var problem = await ApiDriver.ProblemAsync(response, HttpStatusCode.Unauthorized);
        problem.GetProperty("code").GetString().ShouldBe("invalid_credentials");
    }

    [Fact]
    public async Task Me_requires_authentication_and_returns_the_current_user()
    {
        using var anonymous = _api.Anonymous();
        await ApiDriver.ProblemAsync(await anonymous.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized);

        var client = await _api.RegisterClientAsync("Karim", "Tazi");
        var me = await client.Http.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiDriver.Json);
        me!.Id.ShouldBe(client.UserId);
        me.LastName.ShouldBe("Tazi");
    }

    [Fact]
    public async Task Tampered_access_token_is_rejected()
    {
        var client = await _api.RegisterClientAsync();
        using var http = _api.Anonymous();
        var tampered = client.AccessToken[..^4] + (client.AccessToken.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        (await http.GetAsync("/api/v1/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_replay_revokes_the_whole_family()
    {
        var client = await _api.RegisterClientAsync();

        var first = await _api.RefreshAsync(client.RefreshToken);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotated = ApiDriver.RefreshCookie(first).ShouldNotBeNull();
        rotated.ShouldNotBe(client.RefreshToken);
        (await ApiDriver.ReadAsync<AuthResponse>(first)).User.Id.ShouldBe(client.UserId);

        // Rejeu de l'ancien jeton : refusé et toute la famille est révoquée…
        var replay = await _api.RefreshAsync(client.RefreshToken);
        (await ApiDriver.ProblemAsync(replay, HttpStatusCode.Unauthorized)).GetProperty("code").GetString().ShouldBe("refresh_reused");

        // … y compris le jeton légitime issu de la rotation.
        (await _api.RefreshAsync(rotated)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_without_cookie_is_unauthorized()
    {
        var response = await _api.RefreshAsync(null);
        (await ApiDriver.ProblemAsync(response, HttpStatusCode.Unauthorized)).GetProperty("code").GetString().ShouldBe("refresh_missing");
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_and_clears_the_cookie()
    {
        var client = await _api.RegisterClientAsync();
        using var http = _api.Anonymous();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", $"refreshToken={client.RefreshToken}");

        var response = await http.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("refreshToken=;", StringComparison.Ordinal));
        var refresh = await _api.RefreshAsync(client.RefreshToken);
        (await ApiDriver.ProblemAsync(refresh, HttpStatusCode.Unauthorized)).GetProperty("code").GetString().ShouldBe("refresh_revoked");
    }

    [Fact]
    public async Task Account_is_locked_after_five_failed_attempts()
    {
        var client = await _api.RegisterClientAsync();
        using var http = _api.Anonymous();
        for (var i = 0; i < 4; i++)
        {
            (await http.PostAsJsonAsync("/api/v1/auth/login", new { email = client.Email, password = "Mauvais1234" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // Le 5e échec déclenche le verrouillage ; le bon mot de passe est ensuite refusé lui aussi.
        (await http.PostAsJsonAsync("/api/v1/auth/login", new { email = client.Email, password = "Mauvais1234" })).StatusCode.ShouldBe(HttpStatusCode.Locked);

        var response = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = client.Email, password = CreditFlowApiFactory.Password });

        var problem = await ApiDriver.ProblemAsync(response, HttpStatusCode.Locked);
        problem.GetProperty("code").GetString().ShouldBe("account_locked");
        problem.GetProperty("title").GetString().ShouldBe("Compte verrouillé");
    }

    [Fact]
    public async Task Disabled_account_loses_its_tokens_and_cannot_log_in()
    {
        var client = await _api.RegisterClientAsync();
        var admin = await _api.StaffAsync(RoleNames.Admin);
        (await client.Http.GetAsync("/api/v1/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var patch = await admin.Http.PatchAsJsonAsync($"/api/v1/admin/users/{client.UserId}", new { isActive = false });
        patch.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.Http.GetAsync("/api/v1/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _api.RefreshAsync(client.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var http = _api.Anonymous();
        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = client.Email, password = CreditFlowApiFactory.Password });
        (await ApiDriver.ProblemAsync(login, HttpStatusCode.Unauthorized)).GetProperty("code").GetString().ShouldBe("account_disabled");
    }

    [Fact]
    public async Task Auth_endpoints_are_rate_limited()
    {
        await using var limited = new RateLimitedFactory();
        using var http = limited.CreateApiClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@test.local", password = "Mauvais1234" })).StatusCode);
        }

        statuses.Take(3).ShouldAllBe(s => s == HttpStatusCode.Unauthorized);
        var blocked = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@test.local", password = "Mauvais1234" });
        await ApiDriver.ProblemAsync(blocked, HttpStatusCode.TooManyRequests);
        statuses[3].ShouldBe(HttpStatusCode.TooManyRequests);
        blocked.Headers.Contains("Retry-After").ShouldBeTrue();

        // Les autres routes ne sont pas concernées par le limiteur d'authentification.
        (await http.GetAsync("/api/v1/products")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("")]
    [InlineData("trop-courte")]
    public async Task Startup_is_refused_without_a_strong_signing_key(string key)
    {
        await using var broken = new ConfiguredFactory(new() { ["Jwt:SigningKey"] = key });
        var ex = Should.Throw<Exception>(() => broken.CreateApiClient());
        ex.ToString().ShouldContain("Jwt:SigningKey");
    }

    private sealed class RateLimitedFactory() : ConfiguredFactory(new() { ["RateLimiting:AuthPermitLimit"] = "3", ["RateLimiting:AuthWindowSeconds"] = "60" });
}

/// <summary>Hôte supplémentaire partageant la base déjà migrée (ne la réinitialise pas).</summary>
public class ConfiguredFactory(Dictionary<string, string?> overrides) : CreditFlowApiFactory(overrides)
{
    public override Task InitializeAsync() => Task.CompletedTask;
}
