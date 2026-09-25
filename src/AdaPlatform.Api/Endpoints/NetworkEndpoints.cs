using AdaPlatform.Api.Auth;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Api.Endpoints;

public static class NetworkEndpoints
{
    /// <summary>
    /// A stop with coordinates, plus how busy it is across all stop visits recorded there
    /// (zero visits = the stop exists in the network but no trip data covers it).
    /// </summary>
    public sealed record StopDto(int Code, string Name, double Latitude, double Longitude, int Visits, int Boardings, int Alightings);

    public sealed record PatternSummaryDto(int Code, string? FirstStopName, string? LastStopName, int StopCount, int Trips);

    public sealed record LineDto(int Id, IReadOnlyList<PatternSummaryDto> Patterns);

    public sealed record PatternStopDto(int Sequence, int Code, string Name, double? Latitude, double? Longitude);

    public static IEndpointRouteBuilder MapNetworkEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(Permissions.NetworkRead);

        // Stops without coordinates can't be drawn, so the map endpoint leaves them out.
        api.MapGet("/stops", async (AppDbContext db, CancellationToken ct) =>
        {
            var activity = await db.StopVisits.AsNoTracking()
                .GroupBy(v => v.StopCode)
                .Select(g => new { Code = g.Key, Visits = g.Count(), Boardings = g.Sum(v => v.Boardings), Alightings = g.Sum(v => v.Alightings) })
                .ToDictionaryAsync(a => a.Code, ct);

            var stops = await db.Stops.AsNoTracking()
                .Where(s => s.Latitude != null && s.Longitude != null)
                .OrderBy(s => s.Code)
                .Select(s => new { s.Code, s.Name, Latitude = s.Latitude!.Value, Longitude = s.Longitude!.Value })
                .ToListAsync(ct);

            return stops.Select(s => activity.TryGetValue(s.Code, out var a)
                ? new StopDto(s.Code, s.Name, s.Latitude, s.Longitude, a.Visits, a.Boardings, a.Alightings)
                : new StopDto(s.Code, s.Name, s.Latitude, s.Longitude, 0, 0, 0));
        });

        // Lines with their patterns, busiest pattern first — the map's line picker.
        api.MapGet("/lines", async (AppDbContext db, CancellationToken ct) =>
        {
            var tripsPerPattern = await db.Trips.AsNoTracking()
                .Where(t => t.PatternCode != null)
                .GroupBy(t => t.PatternCode!.Value)
                .Select(g => new { Code = g.Key, Trips = g.Count() })
                .ToDictionaryAsync(p => p.Code, p => p.Trips, ct);

            var patterns = await db.Patterns.AsNoTracking()
                .Select(p => new { p.LineId, p.Code, p.FirstStopName, p.LastStopName, StopCount = p.Stops.Count })
                .ToListAsync(ct);

            return patterns
                .GroupBy(p => p.LineId)
                .OrderBy(g => g.Key)
                .Select(g => new LineDto(g.Key, g
                    .Select(p => new PatternSummaryDto(p.Code, p.FirstStopName, p.LastStopName, p.StopCount, tripsPerPattern.GetValueOrDefault(p.Code)))
                    .OrderByDescending(p => p.Trips).ThenBy(p => p.Code)
                    .ToList()));
        });

        api.MapGet("/patterns/{code:int}/stops", async (int code, AppDbContext db, CancellationToken ct) =>
        {
            var stops = await db.PatternStops.AsNoTracking()
                .Where(ps => ps.PatternCode == code)
                .OrderBy(ps => ps.Sequence)
                .Select(ps => new PatternStopDto(ps.Sequence, ps.StopCode, ps.Stop.Name, ps.Stop.Latitude, ps.Stop.Longitude))
                .ToListAsync(ct);
            return stops.Count == 0 ? Results.NotFound() : Results.Ok(stops);
        });

        return app;
    }
}
