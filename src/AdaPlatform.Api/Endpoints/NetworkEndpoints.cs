using AdaPlatform.Api.Auth;
using AdaPlatform.Infrastructure.Persistence;
using AdaPlatform.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Api.Endpoints;

public static class NetworkEndpoints
{
    /// <summary>
    /// A stop with coordinates, plus how busy it is across all stop visits recorded there
    /// (zero visits = the stop exists in the network but no trip data covers it).
    /// </summary>
    /// <param name="Toward">The most frequent destination of trips calling here (which direction this post serves).</param>
    /// <param name="Bearing">Compass direction (0 = north) towards the most frequent next stop.</param>
    public sealed record StopDto(
        int Code, string Name, double Latitude, double Longitude, int Visits, int Boardings, int Alightings, string? Toward, double? Bearing);

    /// <summary>A route of a line: its patterns with the same stops (timetable variants) as one entry.</summary>
    /// <param name="Code">The busiest variant's code: draws the route (GET /api/patterns/{code}/stops).</param>
    /// <param name="Variants">The timetable versions, with the hours their trips start in.</param>
    /// <param name="ExtraStops">Stops it calls at that the busiest route between the same termini doesn't.</param>
    /// <param name="MissingStops">Stops the busiest route between the same termini calls at that it leaves out.</param>
    public sealed record PatternSummaryDto(
        int Code, string? FirstStopName, string? LastStopName, int StopCount, int Trips,
        IReadOnlyList<PatternVariant> Variants, IReadOnlyList<string> ExtraStops, IReadOnlyList<string> MissingStops);

    public sealed record LineDto(int Id, IReadOnlyList<PatternSummaryDto> Patterns);

    public sealed record PatternStopDto(int Sequence, int Code, string Name, double? Latitude, double? Longitude);

    public static IEndpointRouteBuilder MapNetworkEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(Permissions.NetworkRead);

        // Stops without coordinates can't be drawn, so the map endpoint leaves them out.
        api.MapGet("/stops", async (AppDbContext db, StopDirections stopDirections, CancellationToken ct) =>
        {
            var directions = await stopDirections.GetAsync(ct);
            var activity = await db.StopVisits.AsNoTracking()
                .GroupBy(v => v.StopCode)
                .Select(g => new { Code = g.Key, Visits = g.Count(), Boardings = g.Sum(v => v.Boardings), Alightings = g.Sum(v => v.Alightings) })
                .ToDictionaryAsync(a => a.Code, ct);

            var stops = await db.Stops.AsNoTracking()
                .Where(s => s.Latitude != null && s.Longitude != null)
                .OrderBy(s => s.Code)
                .Select(s => new { s.Code, s.Name, Latitude = s.Latitude!.Value, Longitude = s.Longitude!.Value })
                .ToListAsync(ct);

            return stops.Select(s =>
            {
                var direction = directions.GetValueOrDefault(s.Code);
                return activity.TryGetValue(s.Code, out var a)
                    ? new StopDto(s.Code, s.Name, s.Latitude, s.Longitude, a.Visits, a.Boardings, a.Alightings, direction?.Toward, direction?.Bearing)
                    : new StopDto(s.Code, s.Name, s.Latitude, s.Longitude, 0, 0, 0, direction?.Toward, direction?.Bearing);
            });
        });

        // Lines with their routes (patterns with the same stops as one), busiest first — the map's line picker.
        api.MapGet("/lines", async (PatternGroups patternGroups, CancellationToken ct) =>
            (await patternGroups.GetAsync(ct))
                .GroupBy(g => g.LineId)
                .OrderBy(g => g.Key)
                .Select(g => new LineDto(g.Key, g
                    .Select(p => new PatternSummaryDto(p.Code, p.FirstStopName, p.LastStopName, p.StopCount, p.Trips, p.Variants, p.ExtraStops, p.MissingStops))
                    .ToList())));

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
