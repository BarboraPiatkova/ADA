using System.ComponentModel;
using System.Text.RegularExpressions;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// Which way vehicles leave a stop post: the most frequent destination of the trips calling there
/// ("towards …") and the compass bearing to the most frequent next stop (the map arrow).
/// </summary>
/// <param name="Bearing">0 = north, clockwise; null when either stop has no position.</param>
public sealed record StopDirection(string? Toward, double? Bearing);

/// <summary>
/// The direction each stop post serves, derived from the recorded trips (reconstructed from the logs
/// or imported from ADA), so a station's posts can be told apart on a map. Depot runs are left out:
/// they don't show the direction passengers travel. Cached per version of the data.
/// </summary>
public sealed partial class StopDirections(AppDbContext db, HybridCache cache)
{
    [ImmutableObject(true)]
    private sealed record Cached(IReadOnlyDictionary<int, StopDirection> ByStop);

    // ADA writes terminus names with the stop code in front ("14901 Purmerendská").
    [GeneratedRegex(@"^\d+\s+")]
    private static partial Regex LeadingCode();

    public async Task<IReadOnlyDictionary<int, StopDirection>> GetAsync(CancellationToken ct = default)
    {
        var version = $"{await db.SourceFiles.MaxAsync(f => (long?)f.Id, ct) ?? 0}.{await db.Trips.MaxAsync(t => (long?)t.Id, ct) ?? 0}";
        return (await cache.GetOrCreateAsync($"network/directions/{version}", async token => new Cached(await BuildAsync(token)), cancellationToken: ct)).ByStop;
    }

    public async Task<IReadOnlyDictionary<int, StopDirection>> BuildAsync(CancellationToken ct = default)
    {
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        var rows = await db.StopVisits.AsNoTracking()
            .Where(v => !v.Trip.IsDepotRun)
            .Select(v => new { v.TripId, v.Sequence, v.StopCode, Toward = v.Trip.Pattern != null ? v.Trip.Pattern.LastStopName : null })
            .ToListAsync(ct);
        var positions = await db.Stops.AsNoTracking()
            .Where(s => s.Latitude != null && s.Longitude != null)
            .Select(s => new { s.Code, Latitude = s.Latitude!.Value, Longitude = s.Longitude!.Value })
            .ToDictionaryAsync(s => s.Code, ct);

        var next = new Dictionary<int, Dictionary<int, int>>();
        var toward = new Dictionary<int, Dictionary<string, int>>();
        static void Count<TKey>(Dictionary<int, Dictionary<TKey, int>> counts, int stop, TKey key) where TKey : notnull
        {
            if (!counts.TryGetValue(stop, out var byKey)) counts[stop] = byKey = [];
            byKey[key] = byKey.GetValueOrDefault(key) + 1;
        }

        foreach (var trip in rows.GroupBy(r => r.TripId))
        {
            var ordered = trip.OrderBy(r => r.Sequence).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                if (i + 1 < ordered.Count && ordered[i + 1].StopCode != ordered[i].StopCode)
                {
                    Count(next, ordered[i].StopCode, ordered[i + 1].StopCode);
                }
                if (ordered[i].Toward is { Length: > 0 } name)
                {
                    Count(toward, ordered[i].StopCode, LeadingCode().Replace(name, "").Trim());
                }
            }
        }

        return next.Keys.Union(toward.Keys).ToDictionary(
            code => code,
            code =>
            {
                double? bearing = next.TryGetValue(code, out var n) && positions.GetValueOrDefault(code) is { } here
                                  && positions.GetValueOrDefault(n.MaxBy(x => x.Value).Key) is { } there
                    ? Math.Round(Bearing(here.Latitude, here.Longitude, there.Latitude, there.Longitude))
                    : null;
                return new StopDirection(toward.TryGetValue(code, out var d) ? d.MaxBy(x => x.Value).Key : null, bearing);
            });
    }

    /// <summary>Initial compass bearing (0 = north, clockwise) from one point to another.</summary>
    private static double Bearing(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double deg) => deg * Math.PI / 180;
        var dLon = Rad(lon2 - lon1);
        var y = Math.Sin(dLon) * Math.Cos(Rad(lat2));
        var x = Math.Cos(Rad(lat1)) * Math.Sin(Rad(lat2)) - Math.Sin(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Cos(dLon);
        return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
    }
}
