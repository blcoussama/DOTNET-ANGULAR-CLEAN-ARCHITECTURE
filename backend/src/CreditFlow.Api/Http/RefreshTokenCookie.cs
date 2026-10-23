namespace CreditFlow.Api.Http;

/// <summary>
/// Cookie httpOnly <c>refreshToken</c> : Path=/api/v1/auth, SameSite=Strict, Secure hors Development
/// (surcharge possible par <c>Auth:RefreshCookieSecure</c>). Le jeton n'est jamais exposé au JavaScript.
/// </summary>
public sealed class RefreshTokenCookie(IWebHostEnvironment environment, IConfiguration configuration)
{
    public const string Name = "refreshToken";
    public const string Path = "/api/v1/auth";

    private bool Secure => configuration.GetValue<bool?>("Auth:RefreshCookieSecure") ?? !environment.IsDevelopment();

    public string? Read(HttpRequest request) => request.Cookies[Name];

    public void Write(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, Options(expiresAt));

    public void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, Options(null));

    private CookieOptions Options(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = Secure,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}
