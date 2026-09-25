using System.Text.Json;
using AdaPlatform.Api.Tests.Infrastructure;
using AdaPlatform.Infrastructure.Import.Ucp;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AdaPlatform.Api.Tests;

/// <summary>How the quality reports are configured and cached.</summary>
public sealed class ReportingTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Thresholds_come_from_configuration()
    {
        await using var api = NewApi().WithWebHostBuilder(b => b.UseSetting("Quality:Thresholds:ImbalanceWarning", "0.5"));

        using var report = JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/quality/devices"));

        var thresholds = report.RootElement.GetProperty("thresholds");
        Assert.Equal(0.5, thresholds.GetProperty("imbalanceWarning").GetDouble());
        Assert.Equal(0.35, thresholds.GetProperty("imbalanceFault").GetDouble()); // untouched default
    }

    [Fact]
    public async Task A_new_import_replaces_the_cached_report()
    {
        await using var api = NewApi();
        using var client = api.CreateSignedInClient();
        Assert.Equal(0, await VehicleCount(client)); // empty database: cached as such

        var folder = UcpLogFixture.WriteToNewFolder();
        try
        {
            await using (var scope = api.Services.CreateAsyncScope())
            {
                await new UcpLogIngestor(scope.ServiceProvider.GetRequiredService<AppDbContext>()).IngestAsync(folder);
            }

            Assert.Equal(1, await VehicleCount(client));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task<int> VehicleCount(HttpClient client)
    {
        using var report = JsonDocument.Parse(await client.GetStringAsync("/api/quality/devices"));
        return report.RootElement.GetProperty("vehicles").GetArrayLength();
    }

    private ApiFactory NewApi() => fixture.NewApi();
}
