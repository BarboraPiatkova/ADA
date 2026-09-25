using AdaPlatform.Infrastructure.Persistence;
using AdaPlatform.Infrastructure.Reporting;
using Microsoft.Extensions.Caching.Memory;

namespace AdaPlatform.Api.Endpoints;

public static class QualityEndpoints
{
    // Aggregating the raw layer takes seconds on a week of data; the raw layer only
    // changes on import, so a short in-memory cache keeps the page instant.
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    public static IEndpointRouteBuilder MapQualityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/quality/devices", async (AppDbContext db, IMemoryCache cache, CancellationToken ct) =>
            await cache.GetOrCreateAsync("quality/devices", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheFor;
                return new DeviceHealthReport(db).BuildAsync(new HealthThresholds(), ct);
            }));

        app.MapGet("/api/quality/daily", async (AppDbContext db, IMemoryCache cache, CancellationToken ct) =>
            await cache.GetOrCreateAsync("quality/daily", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheFor;
                return new DailyQualityReport(db).BuildAsync(new HealthThresholds(), ct);
            }));

        return app;
    }
}
