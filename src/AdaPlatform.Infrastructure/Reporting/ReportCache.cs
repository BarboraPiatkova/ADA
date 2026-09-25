using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// Caches a report per version of the data. The raw layer only changes on import, so the
/// key is the newest imported file: after an import (API or CLI) the next request rebuilds,
/// otherwise the cached report stays valid. HybridCache runs one build for concurrent
/// first requests (no stampede) and keeps it going while any of them still waits.
/// </summary>
internal static class ReportCache
{
    private static readonly HybridCacheEntryOptions Entry = new()
    {
        // Old versions just age out; a new import already changes the key.
        Expiration = TimeSpan.FromHours(12),
        LocalCacheExpiration = TimeSpan.FromHours(12),
    };

    public static async Task<T> GetAsync<T>(
        AppDbContext db, HybridCache cache, string name, Func<CancellationToken, Task<T>> build, CancellationToken ct)
    {
        var version = await db.SourceFiles.MaxAsync(f => (long?)f.Id, ct) ?? 0;
        return await cache.GetOrCreateAsync($"{name}/{version}", async token => await build(token), Entry, cancellationToken: ct);
    }
}
