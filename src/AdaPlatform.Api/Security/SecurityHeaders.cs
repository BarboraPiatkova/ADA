namespace AdaPlatform.Api.Security;

/// <summary>
/// Response headers that tell the browser to refuse what this app never does: framing,
/// MIME sniffing, scripts or styles from anywhere but the app's own origin.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// The SPA's needs, and nothing more. Scripts and styles come only from the app's own
    /// origin (index.html has no inline script, see public/theme-init.js); images also from
    /// the OSM tile server and the Mapy.com logo. Mapy.com tiles come through our own proxy,
    /// so they are 'self'.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "img-src 'self' data: https://tile.openstreetmap.org https://api.mapy.com; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            await next();
        });
}
