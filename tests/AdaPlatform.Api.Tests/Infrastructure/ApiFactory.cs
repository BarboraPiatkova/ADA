using System.Net.Http.Headers;
using AdaPlatform.Api.Auth;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AdaPlatform.Api.Tests.Infrastructure;

/// <summary>The real API, pointed at a given engine and database, migrating on startup.</summary>
public sealed class ApiFactory(DatabaseProvider provider, string connectionString) : WebApplicationFactory<Program>
{
    public static readonly Uri TokariUrl = new("http://tokari.test/");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = provider.ToString(),
                ["Database:MigrateOnStartup"] = "true",
                ["ConnectionStrings:Default"] = connectionString,
                ["Tokari:BaseUrl"] = TokariUrl.ToString(),
                ["Tokari:Issuer"] = TokariTokens.Issuer,
                ["Tokari:Audience"] = TokariTokens.Audience,
                ["Tokari:SigningKey"] = TokariTokens.SigningKey,
                // Never the developer's real Mapy.com key from user-secrets: a test must not
                // spend credits. Tests that need a key set a fake one.
                ["Map:MapyComApiKey"] = "",
            });
        });
    }
}

public static class ApiClients
{
    /// <summary>A client signed in as a user with all of AdaPlatform's permissions.</summary>
    public static HttpClient CreateSignedInClient(this WebApplicationFactory<Program> api) =>
        api.CreateSignedInClient([.. Permissions.All]);

    public static HttpClient CreateSignedInClient(this WebApplicationFactory<Program> api, params string[] permissions)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokariTokens.For(permissions));
        return client;
    }
}
