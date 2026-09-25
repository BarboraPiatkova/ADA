using System.Net;
using AdaPlatform.Api.Security;
using AdaPlatform.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AdaPlatform.Api.Tests;

/// <summary>What every response promises the browser, and how the API fails.</summary>
public sealed class HttpSecurityTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Responses_carry_the_security_headers()
    {
        await using var api = NewApi();
        var response = await api.CreateSignedInClient().GetAsync("/api/map/config");

        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task An_unknown_route_answers_with_problem_details()
    {
        await using var api = NewApi();
        var response = await api.CreateSignedInClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_tile_proxy_rejects_a_client_over_its_budget()
    {
        await using var api = NewApi();
        using var client = api.CreateSignedInClient();

        // No key is configured, so each request is a cheap 404 — but it still counts.
        for (var i = 0; i < 600; i++)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/map/tiles/basic/256/0/0/0")).StatusCode);
        }

        var over = await client.GetAsync("/api/map/tiles/basic/256/0/0/0");
        Assert.Equal(HttpStatusCode.TooManyRequests, over.StatusCode);

        // Other endpoints have their own (no) limit.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/map/config")).StatusCode);
    }

    private WebApplicationFactory<Program> NewApi() =>
        new ApiFactory(fixture.Provider, fixture.NewDatabaseConnectionString()).WithWebHostBuilder(builder =>
            builder.UseSetting("Map:MapyComApiKey", ""));
}
