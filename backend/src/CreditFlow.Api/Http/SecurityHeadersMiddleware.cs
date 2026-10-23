namespace CreditFlow.Api.Http;

/// <summary>En-têtes de sécurité HTTP appliqués à toutes les réponses de l'API.</summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        if (!context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase))
        {
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        }

        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            headers.CacheControl = "no-store";
        }

        return next(context);
    }
}
