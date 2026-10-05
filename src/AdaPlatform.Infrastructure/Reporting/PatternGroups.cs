using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// Patterns that are one route to a reader. A pattern code is line · route · variant (100102 = line 1,
/// route 001, variant 02), and the variants of a route are mostly timetable versions of it: the same stops,
/// run at other hours with other running times. Patterns of a line with the same stops in the same order
/// are one group, shown once with its variants' hours; a variant that calls at other stops (a turning loop,
/// a skipped stop) stays a group of its own, labelled with the stops it adds or leaves out compared with
/// the busiest group between the same termini. A pattern without a stop list stays alone.
/// </summary>
public sealed class PatternGroups(AppDbContext db)
{
    public async Task<IReadOnlyList<PatternGroup>> GetAsync(CancellationToken ct = default)
    {
        var patterns = await db.Patterns.AsNoTracking()
            .Select(p => new { p.Code, p.LineId, p.FirstStopName, p.LastStopName })
            .ToListAsync(ct);
        var stops = (await db.PatternStops.AsNoTracking()
                .Select(ps => new { ps.PatternCode, ps.Sequence, ps.StopCode })
                .ToListAsync(ct))
            .GroupBy(ps => ps.PatternCode)
            .ToDictionary(g => g.Key, g => g.OrderBy(ps => ps.Sequence).Select(ps => ps.StopCode).ToList());
        var trips = await db.Trips.AsNoTracking()
            .Where(t => t.PatternCode != null)
            .GroupBy(t => t.PatternCode!.Value)
            .Select(g => new { Code = g.Key, Trips = g.Count(), FirstHour = g.Min(t => t.StartTime.Hour), LastHour = g.Max(t => t.StartTime.Hour) })
            .ToDictionaryAsync(x => x.Code, ct);
        var names = await db.Stops.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Name, ct);

        var groups = patterns
            .GroupBy(p => stops.TryGetValue(p.Code, out var list) && list.Count > 0 ? $"{p.LineId}:{string.Join(',', list)}" : $"alone:{p.Code}")
            .Select(g =>
            {
                var variants = g
                    .Select(p => trips.TryGetValue(p.Code, out var x)
                        ? new PatternVariant(p.Code, x.Trips, x.FirstHour, x.LastHour)
                        : new PatternVariant(p.Code, 0, null, null))
                    .OrderBy(v => v.Code)
                    .ToList();
                // The variant with most trips names the group (its code draws the route and opens the profile).
                var lead = g.OrderByDescending(p => trips.GetValueOrDefault(p.Code)?.Trips ?? 0).ThenBy(p => p.Code).First();
                return new
                {
                    lead.Code,
                    lead.LineId,
                    lead.FirstStopName,
                    lead.LastStopName,
                    Stops = stops.GetValueOrDefault(lead.Code) ?? [],
                    Variants = variants,
                };
            })
            .ToList();

        // Against the busiest group between the same termini on the same line: which stops it adds or leaves out.
        var mains = groups.GroupBy(g => (g.LineId, g.FirstStopName, g.LastStopName))
            .ToDictionary(x => x.Key, x => x.OrderByDescending(g => g.Variants.Sum(v => v.Trips)).ThenBy(g => g.Code).First());
        return groups
            .Select(g =>
            {
                var main = mains[(g.LineId, g.FirstStopName, g.LastStopName)];
                var extra = main.Code == g.Code ? [] : Difference(g.Stops, main.Stops, names);
                var missing = main.Code == g.Code ? [] : Difference(main.Stops, g.Stops, names);
                return new PatternGroup(
                    g.Code, g.LineId, Clean(g.FirstStopName), Clean(g.LastStopName), g.Stops.Count, g.Variants.Sum(v => v.Trips),
                    g.Variants, extra, missing);
            })
            .OrderBy(g => g.LineId).ThenByDescending(g => g.Trips).ThenBy(g => g.Code)
            .ToList();
    }

    /// <summary>The names of the calls in <paramref name="these"/> that <paramref name="others"/> doesn't make (a stop called twice counts twice).</summary>
    private static List<string> Difference(List<int> these, List<int> others, Dictionary<int, string> names)
    {
        var left = others.GroupBy(c => c).ToDictionary(x => x.Key, x => x.Count());
        var result = new List<string>();
        foreach (var code in these)
        {
            if (left.TryGetValue(code, out var n) && n > 0)
            {
                left[code] = n - 1;
                continue;
            }
            var name = names.GetValueOrDefault(code) ?? code.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!result.Contains(name))
            {
                result.Add(name);
            }
        }
        return result;
    }

    // ADA writes terminus names with the stop code in front ("14901 Purmerendská").
    private static string? Clean(string? name) => name is null ? null : System.Text.RegularExpressions.Regex.Replace(name, @"^\d+\s+", "");
}

/// <summary>One timetable version of a route: its own code, trips and the hours its trips start in.</summary>
public sealed record PatternVariant(int Code, int Trips, int? FirstHour, int? LastHour);

/// <param name="Code">The busiest variant's code: it stands for the group (route drawing, occupancy profile).</param>
/// <param name="Trips">Trips of every variant.</param>
/// <param name="ExtraStops">Stops this group calls at that the busiest group between the same termini doesn't.</param>
/// <param name="MissingStops">Stops the busiest group between the same termini calls at that this one leaves out.</param>
public sealed record PatternGroup(
    int Code, int LineId, string? FirstStopName, string? LastStopName, int StopCount, int Trips,
    IReadOnlyList<PatternVariant> Variants, IReadOnlyList<string> ExtraStops, IReadOnlyList<string> MissingStops)
{
    public bool Contains(int pattern) => Variants.Any(v => v.Code == pattern);
}
