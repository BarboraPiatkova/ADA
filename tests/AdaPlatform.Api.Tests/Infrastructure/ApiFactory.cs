using AdaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AdaPlatform.Api.Tests.Infrastructure;

/// <summary>The real API, pointed at a given engine and database, migrating on startup.</summary>
public sealed class ApiFactory(DatabaseProvider provider, string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = provider.ToString(),
                ["Database:MigrateOnStartup"] = "true",
                ["ConnectionStrings:Default"] = connectionString,
            });
        });
    }
}
