using System.Threading.RateLimiting;

namespace AdaPlatform.Api.Security;

/// <summary>
/// Per-client request limits. The tile proxy spends the operator's Mapy.com credits on
/// every cache miss, so it gets its own budget: generous for panning a map, too small
/// for someone scraping the tile pyramid through us.
/// </summary>
public static class RateLimits
{
    public const string Tiles = "tiles";

    public static IServiceCollection AddAdaPlatformRateLimits(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Partitioned by client IP. Behind a reverse proxy this needs the forwarded
            // headers middleware, or every user shares the proxy's address.
            options.AddPolicy(Tiles, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    // One full-screen view at retina resolution is ~60 tiles.
                    PermitLimit = 600,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
}
