using System.Net;
using Microsoft.Extensions.Options;

namespace AdaPlatform.Api.Map;

/// <summary>Bound from the "Map" configuration section — set per deployment.</summary>
public sealed class MapOptions
{
    public const string SectionName = "Map";

    /// <summary>
    /// Mapy.com REST API key. Never sent to the browser: tiles go through
    /// <c>/api/map/tiles</c>, which adds it server-side. Without a key only
    /// OpenStreetMap is offered.
    /// </summary>
    public string? MapyComApiKey { get; set; }

    /// <summary>Where proxied tiles are cached on disk (relative to the content root).</summary>
    public string TileCacheFolder { get; set; } = "tile-cache";
}

public static class MapEndpoints
{
    public const string MapyHttpClient = "mapy.com";

    // Mapy.com answers tiles with Cache-Control: max-age=86400 (checked 2026-09-25);
    // the proxy keeps tiles no longer than that.
    private static readonly TimeSpan TileLifetime = TimeSpan.FromDays(1);

    private static readonly Dictionary<string, (string Extension, string ContentType)> MapySets = new()
    {
        ["basic"] = (".png", "image/png"),
        ["outdoor"] = (".png", "image/png"),
        ["aerial"] = (".jpg", "image/jpeg"),
        ["winter"] = (".png", "image/png"),
    };

    private const string MapyAttribution =
        "<a href=\"https://api.mapy.com/copyright\" target=\"_blank\" rel=\"noopener\">&copy; Seznam.cz a.s. a další</a>";

    /// <summary>A base map. No display name: the UI names layers by <see cref="Id"/> in its own language.</summary>
    public sealed record BaseLayerDto(string Id, string Url, string Attribution, int MaxZoom, bool RequiresMapyLogo);

    public sealed record MapConfigDto(IReadOnlyList<BaseLayerDto> BaseLayers);

    public static IServiceCollection AddMapTiles(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MapOptions>().Bind(configuration.GetSection(MapOptions.SectionName));
        services.AddHttpClient(MapyHttpClient, client =>
        {
            client.BaseAddress = new Uri("https://api.mapy.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }

    public static IEndpointRouteBuilder MapMapEndpoints(this IEndpointRouteBuilder app)
    {
        var map = app.MapGroup("/api/map");

        map.MapGet("/config", (IOptions<MapOptions> options) =>
        {
            var layers = new List<BaseLayerDto>();
            if (!string.IsNullOrWhiteSpace(options.Value.MapyComApiKey))
            {
                // {r} is Leaflet's retina placeholder ("@2x" on high-DPI screens).
                layers.AddRange(MapySets.Select(set => new BaseLayerDto(
                    $"mapy-{set.Key}", $"/api/map/tiles/{set.Key}/256{{r}}/{{z}}/{{x}}/{{y}}",
                    MapyAttribution, MaxZoom: 19, RequiresMapyLogo: true)));
            }

            // Fallback that needs no key. The public OSM tile server is for light use only
            // (see its tile usage policy), so it's not the default when a Mapy.com key exists.
            layers.Add(new BaseLayerDto(
                "osm", "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
                "&copy; <a href=\"https://www.openstreetmap.org/copyright\" target=\"_blank\" rel=\"noopener\">OpenStreetMap</a>",
                MaxZoom: 19, RequiresMapyLogo: false));

            return new MapConfigDto(layers);
        });

        map.MapGet("/tiles/{mapset}/{size}/{z:int}/{x:int}/{y:int}", GetTileAsync);

        return app;
    }

    private static async Task<IResult> GetTileAsync(
        string mapset, string size, int z, int x, int y,
        IOptions<MapOptions> options, IHttpClientFactory httpClients, IWebHostEnvironment env,
        HttpContext http, CancellationToken ct)
    {
        var apiKey = options.Value.MapyComApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || !MapySets.TryGetValue(mapset, out var set)
            || size is not ("256" or "256@2x") || z is < 0 or > 20
            || x < 0 || y < 0 || x >= 1 << z || y >= 1 << z)
        {
            return Results.NotFound();
        }

        // Disk cache: the same tile is fetched (and paid for) once per lifetime,
        // however many users view it.
        var cacheFile = Path.Combine(env.ContentRootPath, options.Value.TileCacheFolder,
            mapset, size.Replace('@', '_'), z.ToString(), x.ToString(), y + set.Extension);
        var cached = new FileInfo(cacheFile);
        if (cached.Exists && DateTime.UtcNow - cached.LastWriteTimeUtc < TileLifetime)
        {
            SetBrowserCache(http, TileLifetime - (DateTime.UtcNow - cached.LastWriteTimeUtc));
            return Results.File(cacheFile, set.ContentType);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/maptiles/{mapset}/{size}/{z}/{x}/{y}");
        // Key in a header, not the query string, so it never lands in access logs.
        request.Headers.Add("X-Mapy-Api-Key", apiKey);

        HttpResponseMessage upstream;
        try
        {
            upstream = await httpClients.CreateClient(MapyHttpClient).SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

        using (upstream)
        {
            if (upstream.StatusCode == HttpStatusCode.NotFound)
            {
                return Results.NotFound();
            }
            if (!upstream.IsSuccessStatusCode)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }

            var bytes = await upstream.Content.ReadAsByteArrayAsync(ct);

            // Write to a temp file and move, so a concurrent reader never sees half a tile.
            Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
            var temp = $"{cacheFile}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(temp, bytes, ct);
            File.Move(temp, cacheFile, overwrite: true);

            SetBrowserCache(http, TileLifetime);
            return Results.Bytes(bytes, set.ContentType);
        }
    }

    private static void SetBrowserCache(HttpContext http, TimeSpan remaining) =>
        http.Response.Headers.CacheControl = $"public, max-age={(int)Math.Max(0, remaining.TotalSeconds)}";
}
