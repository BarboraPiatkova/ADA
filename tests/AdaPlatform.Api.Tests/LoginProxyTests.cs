using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdaPlatform.Api.Auth;
using AdaPlatform.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AdaPlatform.Api.Tests;

/// <summary>
/// The login proxy in front of Tokari. Tokari is a fake that answers like the real one
/// (Tokari.API/Controllers/AuthController.cs); the browser is an HttpClient with cookies.
/// </summary>
public sealed class LoginProxyTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly FakeTokari _tokari = new();

    [Fact]
    public async Task Login_returns_the_access_token_and_keeps_the_refresh_token_in_an_HttpOnly_cookie()
    {
        await using var api = NewApi();
        using var browser = Browser(api);

        var response = await browser.PostAsJsonAsync("/api/auth/login", new { userName = "dispecer", password = "correct" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<AuthEndpoints.SessionDto>();
        Assert.Equal(_tokari.LastAccessToken, session!.AccessToken);
        Assert.Equal("dispecer", session.User.Name);
        Assert.Equal([Permissions.NetworkRead, Permissions.QualityRead], session.User.Permissions);
        Assert.InRange(session.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(4), DateTimeOffset.UtcNow.AddMinutes(6));

        // The refresh token never reaches JavaScript: not in the body, only in the cookie.
        Assert.DoesNotContain(_tokari.LastRefreshToken, await response.Content.ReadAsStringAsync());
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith($"{AuthEndpoints.RefreshCookie}={Uri.EscapeDataString(_tokari.LastRefreshToken)}", cookie);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);

        var login = Assert.Single(_tokari.Requests);
        Assert.Equal("/api/auth/login", login.Path);
        Assert.Equal("dispecer", login.Body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Wrong_credentials_answer_401()
    {
        await using var api = NewApi();
        using var browser = Browser(api);

        var response = await browser.PostAsJsonAsync("/api/auth/login", new { userName = "dispecer", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => !c.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task A_Tokari_user_without_access_to_AdaPlatform_gets_403_and_their_new_session_is_revoked()
    {
        _tokari.Apps = [new TokariTokens.App("Transportella", "transportella", ["Dispatcher"], ["dispatcher"])];
        await using var api = NewApi();
        using var browser = Browser(api);

        var response = await browser.PostAsJsonAsync("/api/auth/login", new { userName = "dispecer", password = "correct" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var logout = Assert.Single(_tokari.Requests, r => r.Path == "/api/auth/logout");
        Assert.Equal(_tokari.LastRefreshToken, logout.Body.GetProperty("refreshToken").GetString());
        Assert.Equal($"Bearer {_tokari.LastAccessToken}", logout.Authorization);
    }

    [Fact]
    public async Task A_token_this_API_cannot_verify_is_not_passed_on()
    {
        _tokari.SigningKey = "a-different-key-than-the-api-has-configured-0123456789";
        await using var api = NewApi();
        using var browser = Browser(api);

        var response = await browser.PostAsJsonAsync("/api/auth/login", new { userName = "dispecer", password = "correct" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain(_tokari.LastAccessToken, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Tokari_being_down_answers_503()
    {
        _tokari.Down = true;
        await using var api = NewApi();
        using var browser = Browser(api);

        var response = await browser.PostAsJsonAsync("/api/auth/login", new { userName = "dispecer", password = "correct" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_trades_the_cookie_for_a_new_session_and_rotates_the_cookie()
    {
        await using var api = NewApi();
        using var browser = Browser(api);
        await browser.PostAsJsonAsync("/api/auth/login", new { userName = "dispecer", password = "correct" });
        var first = _tokari.LastRefreshToken;

        var response = await Post(browser, "/api/auth/refresh");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refresh = Assert.Single(_tokari.Requests, r => r.Path == "/api/auth/refresh");
        Assert.Equal(first, refresh.Body.GetProperty("refreshToken").GetString());
        Assert.StartsWith($"{AuthEndpoints.RefreshCookie}={Uri.EscapeDataString(_tokari.LastRefreshToken)}", response.Headers.GetValues("Set-Cookie").Single());
        Assert.NotEqual(first, _tokari.LastRefreshToken);
    }

    [Fact]
    public async Task Refresh_also_checks_access_to_AdaPlatform()
    {
        await using var api = NewApi();
        using var browser = Browser(api);
        await browser.PostAsJsonAsync("/api/auth/login", new { userName = "dispecer", password = "correct" });

        // An administrator removes the user's AdaPlatform role; the next refresh must notice.
        _tokari.Apps = [new TokariTokens.App("Transportella", "transportella", ["Dispatcher"], ["dispatcher"])];
        var response = await Post(browser, "/api/auth/refresh");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_answers_401()
    {
        await using var api = NewApi();
        using var browser = Browser(api);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(browser, "/api/auth/refresh")).StatusCode);
        Assert.Empty(_tokari.Requests);
    }

    [Fact]
    public async Task Cookie_endpoints_refuse_requests_without_the_CSRF_header()
    {
        await using var api = NewApi();
        using var browser = Browser(api);
        await browser.PostAsJsonAsync("/api/auth/login", new { userName = "dispecer", password = "correct" });

        var refresh = await browser.PostAsync("/api/auth/refresh", null);
        var logout = await browser.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.BadRequest, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, logout.StatusCode);
        Assert.DoesNotContain(_tokari.Requests, r => r.Path != "/api/auth/login");
    }

    [Fact]
    public async Task Logout_revokes_the_session_in_Tokari_and_clears_the_cookie()
    {
        await using var api = NewApi();
        using var browser = Browser(api);
        await browser.PostAsJsonAsync("/api/auth/login", new { userName = "dispecer", password = "correct" });

        var response = await Post(browser, "/api/auth/logout");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["/api/auth/login", "/api/auth/refresh", "/api/auth/logout"], _tokari.Requests.Select(r => r.Path));
        Assert.Equal(_tokari.LastRefreshToken, _tokari.Requests[^1].Body.GetProperty("refreshToken").GetString());
        Assert.Contains("expires=Thu, 01 Jan 1970", response.Headers.GetValues("Set-Cookie").Single(), StringComparison.OrdinalIgnoreCase);

        // The cookie is gone: the next refresh has nothing to trade.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(browser, "/api/auth/refresh")).StatusCode);
    }

    private static Task<HttpResponseMessage> Post(HttpClient browser, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(AuthEndpoints.CsrfHeader, "fetch");
        return browser.SendAsync(request);
    }

    /// <summary>HTTPS, so the client's cookie jar keeps and sends the Secure cookie.</summary>
    private static HttpClient Browser(WebApplicationFactory<Program> api) =>
        api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    private WebApplicationFactory<Program> NewApi() =>
        new ApiFactory(fixture.Provider, fixture.NewDatabaseConnectionString()).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddHttpClient<TokariClient>().ConfigurePrimaryHttpMessageHandler(() => _tokari)));

    /// <summary>Tokari's /api/auth endpoints: status codes and bodies as the real controller returns them.</summary>
    private sealed class FakeTokari : HttpMessageHandler
    {
        public sealed record Call(string Path, JsonElement Body, string? Authorization);

        public List<Call> Requests { get; } = [];
        public TokariTokens.App[] Apps { get; set; } =
            [TokariTokens.AdaPlatform(Permissions.NetworkRead, Permissions.QualityRead), new("Transportella", "transportella", ["Dispatcher"], ["dispatcher"])];
        public string SigningKey { get; set; } = TokariTokens.SigningKey;
        public bool Down { get; set; }
        public string LastAccessToken { get; private set; } = "";
        public string LastRefreshToken { get; private set; } = "";

        private readonly HashSet<string> _validRefreshTokens = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (Down)
            {
                throw new HttpRequestException("Connection refused");
            }

            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
            Requests.Add(new Call(request.RequestUri!.AbsolutePath, body, request.Headers.Authorization?.ToString()));

            switch (request.RequestUri.AbsolutePath)
            {
                case "/api/auth/login":
                    return body.GetProperty("password").GetString() == "correct"
                        ? Tokens()
                        : new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("Invalid credentials.") };

                case "/api/auth/refresh":
                    // Rotation: a refresh token works once.
                    return _validRefreshTokens.Remove(body.GetProperty("refreshToken").GetString()!)
                        ? Tokens()
                        : new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("Invalid or expired refresh token.") };

                case "/api/auth/logout":
                    if (request.Headers.Authorization is null)
                    {
                        return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    }
                    _validRefreshTokens.Remove(body.GetProperty("refreshToken").GetString()!);
                    return new HttpResponseMessage(HttpStatusCode.OK);

                default:
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        private HttpResponseMessage Tokens()
        {
            LastAccessToken = TokariTokens.Create(Apps, key: SigningKey);
            LastRefreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            _validRefreshTokens.Add(LastRefreshToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { accessToken = LastAccessToken, refreshToken = LastRefreshToken }),
            };
        }
    }
}
