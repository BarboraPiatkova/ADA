using System.Net;
using System.Net.Http.Json;
using AdaPlatform.Api.Map;
using AdaPlatform.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdaPlatform.Api.Tests;

/// <summary>
/// The Mapy.com proxy keeps the API key server-side and fetches each tile once per lifetime.
/// Mapy.com itself is replaced by a fake handler — no network, no credits used.
/// </summary>
public sealed class MapTileProxyTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IDisposable
{
    private const string Key = "test-key-not-real";
    private readonly string _cacheFolder = Path.Combine(Path.GetTempPath(), $"tile-cache-{Guid.NewGuid():N}");
    private readonly FakeMapy _mapy = new();

    [Fact]
    public async Task Without_a_key_only_OpenStreetMap_is_offered()
    {
        await using var api = NewApi(apiKey: null);
        var config = await api.CreateSignedInClient().GetFromJsonAsync<MapEndpoints.MapConfigDto>("/api/map/config");

        Assert.Equal(["osm"], config!.BaseLayers.Select(l => l.Id));
    }

    [Fact]
    public async Task With_a_key_Mapy_layers_point_at_the_proxy_and_never_expose_the_key()
    {
        await using var api = NewApi(Key);
        var response = await api.CreateSignedInClient().GetStringAsync("/api/map/config");

        Assert.Contains("/api/map/tiles/basic/256{r}/{z}/{x}/{y}", response);
        Assert.DoesNotContain(Key, response);
    }

    [Fact]
    public async Task A_tile_is_fetched_once_with_the_key_in_a_header_then_served_from_cache()
    {
        await using var api = NewApi(Key);
        using var client = api.CreateClient();

        var first = await client.GetAsync("/api/map/tiles/basic/256/12/2234/1419");
        var second = await client.GetAsync("/api/map/tiles/basic/256/12/2234/1419");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("image/png", second.Content.Headers.ContentType?.MediaType);
        Assert.Equal(FakeMapy.Tile, await second.Content.ReadAsByteArrayAsync());

        var upstream = Assert.Single(_mapy.Requests);
        Assert.Equal("/v1/maptiles/basic/256/12/2234/1419", upstream.RequestUri!.AbsolutePath);
        Assert.Equal(Key, upstream.Headers.GetValues("X-Mapy-Api-Key").Single());
        Assert.DoesNotContain(Key, upstream.RequestUri.Query);
    }

    [Fact]
    public async Task A_tile_is_served_even_when_the_cache_cannot_be_written()
    {
        // The cache folder path is taken by a file, so nothing can be cached there
        // (as with a volume the app's user doesn't own).
        await File.WriteAllTextAsync(_cacheFolder, "not a folder");
        await using var api = NewApi(Key);

        var response = await api.CreateClient().GetAsync("/api/map/tiles/basic/256/5/17/10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(FakeMapy.Tile, await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("/api/map/tiles/unknown/256/1/0/0")]    // not a Mapy.com map set
    [InlineData("/api/map/tiles/basic/512/1/0/0")]      // unsupported tile size
    [InlineData("/api/map/tiles/basic/256/2/4/0")]      // x outside the zoom level
    public async Task Invalid_tile_requests_are_rejected_without_calling_Mapy(string path)
    {
        await using var api = NewApi(Key);
        var response = await api.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_mapy.Requests);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheFolder))
        {
            Directory.Delete(_cacheFolder, recursive: true);
        }
        else if (File.Exists(_cacheFolder))
        {
            File.Delete(_cacheFolder);
        }
    }

    private WebApplicationFactory<Program> NewApi(string? apiKey) =>
        new ApiFactory(fixture.Provider, fixture.NewDatabaseConnectionString()).WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Empty string overrides a key from local user-secrets.
                ["Map:MapyComApiKey"] = apiKey ?? "",
                ["Map:TileCacheFolder"] = _cacheFolder,
            }));
            builder.ConfigureTestServices(services =>
                services.AddHttpClient(MapEndpoints.MapyHttpClient).ConfigurePrimaryHttpMessageHandler(() => _mapy));
        });

    private sealed class FakeMapy : HttpMessageHandler
    {
        public static readonly byte[] Tile = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Tile) });
        }
    }
}
