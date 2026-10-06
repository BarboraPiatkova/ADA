using System.ComponentModel;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// How full vehicles are: boardings over the day, the load profile of a pattern stop by stop, and the
/// most crowded trips. Only valid trips count (every door counted), and depot runs are left out.
///
/// The load on board is the running sum of boardings minus alightings from the trip's start, floored
/// at zero: the vehicle's own on-board figure carries drift from earlier trips (report F6). Occupancy as
/// a share of capacity is given where the fleet register knows the vehicle's capacity.
/// </summary>
public sealed class LoadReport(AppDbContext db, HybridCache cache, DayCalendar calendar)
{
    private const int CrowdedTripsListed = 200;

    [ImmutableObject(true)]
    private sealed record Cached(LoadReportDto Report);

    public async Task<LoadReportDto> GetAsync(int? line, int? pattern, ReportPeriod period = default, CancellationToken ct = default)
    {
        var version = $"{await db.SourceFiles.MaxAsync(f => (long?)f.Id, ct) ?? 0}.{await db.Trips.MaxAsync(t => (long?)t.Id, ct) ?? 0}";
        var key = $"operations/load/{version}/{line?.ToString() ?? "all"}/{pattern?.ToString() ?? "all"}/{period.Key}";
        return (await cache.GetOrCreateAsync(key, async token => new Cached(await BuildAsync(line, pattern, period, token)), cancellationToken: ct)).Report;
    }

    public async Task<LoadReportDto> BuildAsync(int? line, int? pattern, ReportPeriod period = default, CancellationToken ct = default)
    {
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        var trips = db.Trips.AsNoTracking().Where(t => t.SourceFileId != null && t.IsValid && !t.IsDepotRun);
        if (line is { } l)
        {
            trips = trips.Where(t => t.Pattern != null && t.Pattern.LineId == l);
        }
        if (period.Start is { } start)
        {
            trips = trips.Where(t => t.StartTime >= start);
        }
        if (period.End is { } end)
        {
            trips = trips.Where(t => t.StartTime < end);
        }

        var rows = await trips
            .Select(t => new
            {
                t.Id,
                t.VehicleId,
                t.StartTime,
                t.PatternCode,
                Line = t.Pattern != null ? (int?)t.Pattern.LineId : null,
                First = t.Pattern != null ? t.Pattern.FirstStopName : null,
                Last = t.Pattern != null ? t.Pattern.LastStopName : null,
                Capacity = t.Vehicle.SeatingCapacity + t.Vehicle.StandingCapacity,
                Visits = t.StopVisits.OrderBy(v => v.Sequence)
                    .Select(v => new { v.StopCode, v.ArrivalTime, v.Boardings, v.Alightings, v.IsPassThrough }).ToList(),
            })
            .ToListAsync(ct);
        rows = rows.Where(t => calendar.Keeps(period.Days, t.StartTime)).ToList();

        var lines = await db.Trips.AsNoTracking()
            .Where(t => t.SourceFileId != null && t.Pattern != null)
            .Select(t => t.Pattern!.LineId).Distinct().OrderBy(x => x).ToListAsync(ct);
        var names = await db.Stops.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Name, ct);

        // Loads per trip, stop by stop.
        var loaded = rows.Select(t =>
        {
            var load = 0;
            var stops = t.Visits.Select(v =>
            {
                load = Math.Max(0, load + v.Boardings - v.Alightings);
                return new LoadedStop(v.StopCode, v.ArrivalTime, v.Boardings, v.Alightings, load, v.IsPassThrough);
            }).ToList();
            return new LoadedTrip(t.Id, t.VehicleId, t.StartTime, t.PatternCode, t.Line, t.First, t.Last, t.Capacity, stops);
        }).ToList();

        var boardingsByHour = loaded.SelectMany(t => t.Stops.Where(s => s.Arrival is not null))
            .GroupBy(s => s.Arrival!.Value.Hour).OrderBy(g => g.Key)
            .Select(g => new LoadHourDto(g.Key, g.Sum(s => s.Boardings), g.Sum(s => s.Alightings)))
            .ToList();

        // Per day of week, with how many such days there were: the screen shows boardings per day.
        var weekdayDays = loaded.GroupBy(t => ReportPeriod.Weekday(t.Start)).ToDictionary(g => g.Key, g => g.Select(t => t.Start.Date).Distinct().Count());
        var byWeekday = loaded.GroupBy(t => ReportPeriod.Weekday(t.Start)).OrderBy(g => g.Key)
            .Select(g => new LoadWeekdayDto(g.Key, weekdayDays[g.Key], g.Sum(t => t.Stops.Sum(s => s.Boardings)), g.Sum(t => t.Stops.Sum(s => s.Alightings))))
            .ToList();
        var byWeekHour = loaded.SelectMany(t => t.Stops.Where(s => s.Arrival is not null).Select(s => (Weekday: ReportPeriod.Weekday(t.Start), s.Arrival!.Value.Hour, s.Boardings)))
            .GroupBy(x => (x.Weekday, x.Hour)).OrderBy(g => g.Key)
            .Select(g => new LoadWeekHourDto(g.Key.Weekday, g.Key.Hour, weekdayDays[g.Key.Weekday], g.Sum(x => x.Boardings)))
            .ToList();

        var patterns = loaded.Where(t => t.PatternCode is not null)
            .GroupBy(t => t.PatternCode!.Value)
            .Select(g => new LoadPatternDto(g.Key, g.First().Line, Clean(g.First().First), Clean(g.First().Last), g.Count()))
            .OrderByDescending(p => p.Trips)
            .ToList();

        // Profile of the chosen pattern; unless one is asked for, the best covered: most stop calls
        // counted (trips x typical stops per trip), so a short pattern with many trips doesn't win.
        var chosen = pattern ?? loaded.Where(t => t.PatternCode is not null)
            .GroupBy(t => t.PatternCode!.Value)
            .OrderByDescending(g => g.Count() * Median(g.Select(t => t.Stops.Count(s => !s.IsPassThrough))))
            .ThenBy(g => g.Key)
            .Select(g => (int?)g.Key).FirstOrDefault();
        var profile = new List<LoadProfileStopDto>();
        if (chosen is { } code)
        {
            var ofPattern = loaded.Where(t => t.PatternCode == code).ToList();
            // Stop order: the pattern's own stop list (from the timetable, or from a trip that ran all of
            // it); failing that, the sequence most trips share. Never the longest trip: a broken trip that
            // shuttled around a terminus has the most stops.
            var order = await db.PatternStops.AsNoTracking().Where(ps => ps.PatternCode == code)
                .OrderBy(ps => ps.Sequence).Select(ps => ps.StopCode).ToListAsync(ct);
            if (order.Count == 0)
            {
                order = ofPattern.GroupBy(t => string.Join(',', t.Stops.Select(s => s.StopCode)))
                    .OrderByDescending(g => g.Count()).FirstOrDefault()?.First().Stops.Select(s => s.StopCode).ToList() ?? [];
            }
            // A loop calls at the same stop twice: the n-th call there matches each trip's n-th call there.
            profile = order.Select((stop, i) =>
            {
                var occurrence = order.Take(i).Count(code => code == stop);
                var at = ofPattern.Select(t => t.Stops.Where(s => s.StopCode == stop).Skip(occurrence).FirstOrDefault())
                    .Where(s => s is not null).Select(s => s!).ToList();
                var loads = at.Select(s => (double)s.Load).ToList();
                return new LoadProfileStopDto(
                    i + 1, stop, names.GetValueOrDefault(stop) ?? "", at.Count,
                    at.Count == 0 ? 0 : Math.Round(at.Average(s => s.Boardings), 1),
                    at.Count == 0 ? 0 : Math.Round(at.Average(s => s.Alightings), 1),
                    Percentile(loads, 0.5), Percentile(loads, 0.9), loads.Count == 0 ? 0 : loads.Max());
            }).ToList();
        }

        var crowded = loaded
            .Select(t =>
            {
                var peak = t.Stops.OrderByDescending(s => s.Load).FirstOrDefault();
                return new CrowdedTripDto(
                    t.Id, t.VehicleId, t.Start, t.Line, t.PatternCode, Clean(t.First), Clean(t.Last),
                    peak?.Load ?? 0, peak is null ? null : names.GetValueOrDefault(peak.StopCode), peak?.StopCode,
                    t.Stops.Sum(s => s.Boardings), t.Capacity,
                    t.Capacity is > 0 && peak is not null ? Math.Round((double)peak.Load / t.Capacity.Value, 2) : null);
            })
            .OrderByDescending(t => t.PeakLoad)
            .Take(CrowdedTripsListed)
            .ToList();

        return new LoadReportDto(
            loaded.Count == 0 ? null : DateOnly.FromDateTime(loaded.Min(t => t.Start)),
            loaded.Count == 0 ? null : DateOnly.FromDateTime(loaded.Max(t => t.Start)),
            line, lines, loaded.Count, loaded.Sum(t => t.Stops.Sum(s => s.Boardings)),
            loaded.Count(t => t.Capacity is > 0),
            boardingsByHour, byWeekday, byWeekHour, patterns, chosen, pattern is null && chosen is not null, profile, crowded,
            await ReportPeriod.DaysWithDataAsync(db, ct));
    }

    // ADA writes terminus names with the stop code in front ("14901 Purmerendská").
    private static string? Clean(string? name) => name is null ? null : System.Text.RegularExpressions.Regex.Replace(name, @"^\d+\s+", "");

    private static int Median(IEnumerable<int> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
    }

    private static double Percentile(List<double> values, double p)
    {
        if (values.Count == 0) return 0;
        values.Sort();
        var rank = p * (values.Count - 1);
        var low = (int)Math.Floor(rank);
        var high = (int)Math.Ceiling(rank);
        return Math.Round(values[low] + (values[high] - values[low]) * (rank - low), 1);
    }

    private sealed record LoadedStop(int StopCode, DateTime? Arrival, int Boardings, int Alightings, int Load, bool IsPassThrough);

    private sealed record LoadedTrip(long Id, int VehicleId, DateTime Start, int? PatternCode, int? Line, string? First, string? Last, int? Capacity, List<LoadedStop> Stops);
}

public sealed record LoadHourDto(int Hour, int Boardings, int Alightings);

/// <param name="Weekday">1 = Monday … 7 = Sunday.</param>
/// <param name="Days">How many of these weekdays the period has with trips.</param>
public sealed record LoadWeekdayDto(int Weekday, int Days, int Boardings, int Alightings);

/// <param name="Days">How many of these weekdays the period has with trips (to give boardings per day).</param>
public sealed record LoadWeekHourDto(int Weekday, int Hour, int Days, int Boardings);

public sealed record LoadPatternDto(int Code, int? Line, string? FirstStopName, string? LastStopName, int Trips);

/// <param name="MedianLoad">Median passengers on board after the stop, over the pattern's trips.</param>
public sealed record LoadProfileStopDto(
    int Sequence, int StopCode, string StopName, int Trips, double MeanBoardings, double MeanAlightings,
    double MedianLoad, double P90Load, double MaxLoad);

/// <param name="PeakShare">Peak load as a share of the vehicle's capacity; null when the capacity is unknown.</param>
public sealed record CrowdedTripDto(
    long TripId, int VehicleId, DateTime Start, int? Line, int? PatternCode, string? FirstStopName, string? LastStopName,
    int PeakLoad, string? PeakStopName, int? PeakStopCode, int Boardings, int? Capacity, double? PeakShare);

/// <param name="TripsWithCapacity">Trips whose vehicle has a known capacity (occupancy in % is possible).</param>
/// <param name="PatternChosenForReader">The profile shows the best covered pattern because none was asked for.</param>
public sealed record LoadReportDto(
    DateOnly? From, DateOnly? To, int? Line, IReadOnlyList<int> Lines, int Trips, int Boardings, int TripsWithCapacity,
    IReadOnlyList<LoadHourDto> BoardingsByHour, IReadOnlyList<LoadWeekdayDto> BoardingsByWeekday, IReadOnlyList<LoadWeekHourDto> BoardingsByWeekHour,
    IReadOnlyList<LoadPatternDto> Patterns, int? Pattern, bool PatternChosenForReader,
    IReadOnlyList<LoadProfileStopDto> Profile, IReadOnlyList<CrowdedTripDto> Crowded, IReadOnlyList<DateOnly> Days);
