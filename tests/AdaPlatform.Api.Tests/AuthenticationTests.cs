using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AdaPlatform.Api.Auth;
using AdaPlatform.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AdaPlatform.Api.Tests;

/// <summary>Which Tokari tokens the API accepts, and what each permission opens.</summary>
public sealed class AuthenticationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Without_a_token_data_endpoints_answer_401()
    {
        await using var api = NewApi();
        using var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/stops")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/quality/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/map/config")).StatusCode);
    }

    [Fact]
    public async Task Health_and_map_tiles_stay_public()
    {
        await using var api = NewApi();
        using var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        // 404: no Mapy.com key in tests — but not 401.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/map/tiles/basic/256/0/0/0")).StatusCode);
    }

    [Fact]
    public async Task A_Tokari_token_with_the_permission_is_accepted()
    {
        await using var api = NewApi();
        var response = await Get(api, "/api/stops", TokariTokens.For(Permissions.NetworkRead));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Permissions_are_matched_regardless_of_case()
    {
        await using var api = NewApi();
        var response = await Get(api, "/api/stops", TokariTokens.For("Network:Read"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_signed_in_user_without_the_permission_gets_403()
    {
        await using var api = NewApi();
        var response = await Get(api, "/api/quality/devices", TokariTokens.For(Permissions.NetworkRead));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Permissions_of_another_application_grant_nothing_here()
    {
        await using var api = NewApi();
        // Same permission name, but granted in Transportella; AdaPlatform is in aud with none.
        var token = TokariTokens.Create([
            TokariTokens.AdaPlatform(),
            new TokariTokens.App("Transportella", "transportella", ["Admin"], [Permissions.NetworkRead]),
        ]);

        Assert.Equal(HttpStatusCode.Forbidden, (await Get(api, "/api/stops", token)).StatusCode);
    }

    public static TheoryData<string, string> RejectedTokens => new()
    {
        { "no AdaPlatform in aud", TokariTokens.Create([new TokariTokens.App("Transportella", "transportella", ["Admin"], [Permissions.NetworkRead])]) },
        { "signed with another key", TokariTokens.Create([TokariTokens.AdaPlatform(Permissions.NetworkRead)], key: "another-key-that-is-long-enough-for-hs256-0123456789") },
        { "another issuer", TokariTokens.Create([TokariTokens.AdaPlatform(Permissions.NetworkRead)], issuer: "Keycloak") },
        { "expired", TokariTokens.Create([TokariTokens.AdaPlatform(Permissions.NetworkRead)], lifetime: TimeSpan.FromMinutes(-2)) },
        { "unsigned (alg none)", Unsigned(TokariTokens.For(Permissions.NetworkRead)) },
        { "garbage", "not-a-jwt" },
    };

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task Tokens_that_fail_validation_are_rejected(string why, string token)
    {
        await using var api = NewApi();
        var response = await Get(api, "/api/stops", token);

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{why}: {response.StatusCode}");
    }

    private static async Task<HttpResponseMessage> Get(WebApplicationFactory<Program> api, string path, string token)
    {
        using var client = api.CreateClientWithToken(token);
        return await client.GetAsync(path);
    }

    /// <summary>The same payload with the header swapped to alg "none" and the signature dropped.</summary>
    private static string Unsigned(string token)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{header}.{token.Split('.')[1]}.";
    }

    private ApiFactory NewApi() => fixture.NewApi();
}
