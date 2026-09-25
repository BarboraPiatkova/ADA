using System.Security.Claims;
using AdaPlatform.Api.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AdaPlatform.Api.Auth;

/// <summary>
/// Login proxy in front of Tokari, as in Herman's other Tokari clients, with two changes
/// (see ADR 0005): the refresh token never reaches JavaScript — it lives in an HttpOnly
/// cookie only this path receives — and every token Tokari returns, on refresh too, is
/// checked for access to this app before the browser gets it.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>__Secure-: the browser only accepts it over HTTPS (or on localhost).</summary>
    public const string RefreshCookie = "__Secure-ada_refresh";

    /// <summary>
    /// Cookie-authenticated calls must carry this header. A cross-site form can't set it, and
    /// a cross-site script can't send it without a CORS preflight this API never grants.
    /// Defence in depth on top of SameSite=Strict.
    /// </summary>
    public const string CsrfHeader = "X-Requested-With";

    private const string CookiePath = "/api/auth";

    public sealed record LoginRequest(string UserName, string Password);

    public sealed record SessionUser(string Id, string Name, string Email, IReadOnlyList<string> Permissions);

    /// <summary>What the SPA keeps in memory: the access token and who it belongs to.</summary>
    public sealed record SessionDto(string AccessToken, DateTimeOffset ExpiresAt, SessionUser User);

    public static IServiceCollection AddTokariLogin(this IServiceCollection services)
    {
        services.AddHttpClient<TokariClient>((provider, client) =>
        {
            client.BaseAddress = provider.GetRequiredService<IOptions<TokariOptions>>().Value.BaseUrl;
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup(CookiePath).AllowAnonymous();

        auth.MapPost("/login", LoginAsync).RequireRateLimiting(RateLimits.Login);
        auth.MapPost("/refresh", RefreshAsync).AddEndpointFilter(RequireCsrfHeader).RequireRateLimiting(RateLimits.Refresh);
        auth.MapPost("/logout", LogoutAsync).AddEndpointFilter(RequireCsrfHeader);

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, TokariClient tokari, IOptions<TokariOptions> options, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "User name and password are required.");
        }

        var result = await tokari.LoginAsync(request.UserName.Trim(), request.Password, ct);
        return await IssueSessionAsync(result, tokari, options.Value, http, ct);
    }

    private static async Task<IResult> RefreshAsync(
        TokariClient tokari, IOptions<TokariOptions> options, HttpContext http, CancellationToken ct)
    {
        if (!http.Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Not signed in.");
        }

        var result = await tokari.RefreshAsync(refreshToken, ct);
        return await IssueSessionAsync(result, tokari, options.Value, http, ct);
    }

    private static async Task<IResult> LogoutAsync(TokariClient tokari, IOptions<TokariOptions> options, HttpContext http, CancellationToken ct)
    {
        // Tokari revokes a refresh token only for a caller with a valid access token. The
        // browser's may have expired, so refresh first and revoke the token that returns.
        if (http.Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
        {
            var refreshed = await tokari.RefreshAsync(refreshToken, ct);
            if (refreshed.Tokens is { } tokens)
            {
                await tokari.LogoutAsync(tokens, ct);
            }
        }

        DeleteCookie(http);
        return Results.NoContent();
    }

    private static async Task<IResult> IssueSessionAsync(
        TokariClient.Result result, TokariClient tokari, TokariOptions options, HttpContext http, CancellationToken ct)
    {
        if (result.Tokens is not { } tokens)
        {
            if (result.Error == TokariClient.Failure.Rejected)
            {
                DeleteCookie(http);
                // One answer for unknown user and wrong password: no account enumeration.
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Wrong user name or password, or the session has ended.");
            }
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The sign-in service is not available.");
        }

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(
            tokens.AccessToken, TokariAuthentication.ValidationParameters(options));

        if (validation.Exception is SecurityTokenInvalidAudienceException)
        {
            // A valid Tokari user without a role in AdaPlatform. Don't leave the session
            // they just opened lying around in Tokari (it counts towards their session cap).
            await tokari.LogoutAsync(tokens, ct);
            DeleteCookie(http);
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "This account has no access to AdaPlatform.");
        }
        if (!validation.IsValid)
        {
            // Tokari issued a token this API can't verify: a key or issuer mismatch in the
            // deployment's configuration. Say so in the log, not to the user.
            http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AuthEndpoints))
                .LogError(validation.Exception, "A token from Tokari failed validation; check Tokari:SigningKey and Tokari:Issuer.");
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The sign-in service is not available.");
        }

        http.Response.Cookies.Append(RefreshCookie, tokens.RefreshToken, RefreshCookieOptions(options.SessionLifetime));

        var user = new ClaimsPrincipal(validation.ClaimsIdentity);
        var permissions = AppAccess.For(user, options.Audience).Permissions;
        var expiresAt = new DateTimeOffset(validation.SecurityToken.ValidTo, TimeSpan.Zero);

        return Results.Ok(new SessionDto(tokens.AccessToken, expiresAt, new SessionUser(
            Id: user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "",
            Name: user.FindFirstValue(ClaimTypes.Name) ?? "",
            Email: user.FindFirstValue(ClaimTypes.Email) ?? "",
            Permissions: permissions)));
    }

    private static void DeleteCookie(HttpContext http) =>
        http.Response.Cookies.Delete(RefreshCookie, RefreshCookieOptions());

    /// <summary>
    /// The refresh cookie's attributes, the same when setting and deleting it (a browser only
    /// deletes a cookie whose path and flags match): JavaScript can't read it, it travels only
    /// over HTTPS, never cross-site, and only to /api/auth.
    /// </summary>
    private static CookieOptions RefreshCookieOptions(TimeSpan? maxAge = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        MaxAge = maxAge,
        IsEssential = true,
    };

    private static ValueTask<object?> RequireCsrfHeader(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.Request.Headers.ContainsKey(CsrfHeader)
            ? next(context)
            : ValueTask.FromResult<object?>(Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Missing {CsrfHeader} header."));
}
