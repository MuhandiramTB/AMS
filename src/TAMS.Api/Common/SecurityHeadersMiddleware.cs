namespace TAMS.Api.Common;

/// <summary>
/// Adds baseline security response headers to every response. (06 §13, OWASP A05.)
/// These close whole classes of attack (clickjacking, MIME-sniffing, referrer
/// leakage, script injection) at negligible cost. HSTS is applied separately by
/// UseHsts() in non-dev.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent MIME-type sniffing.
        headers["X-Content-Type-Options"] = "nosniff";
        // Disallow framing (clickjacking); CSP frame-ancestors is the modern form.
        headers["X-Frame-Options"] = "DENY";
        // Limit referrer leakage.
        headers["Referrer-Policy"] = "no-referrer";
        // Lock down what the browser may load. This API returns JSON, not HTML,
        // so a restrictive default-src is safe; the SPA is served separately.
        headers["Content-Security-Policy"] =
            "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
        // Disable powerful browser features by default.
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";

        await _next(context);
    }
}
