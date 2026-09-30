using System.ComponentModel;
using System.Globalization;
using AdaPlatform.Infrastructure.Import.Epcomp;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>Limits for judging a departure ("Operations:Punctuality"); Czech practice by default.</summary>
public sealed record PunctualityRules
{
    public const string SectionName = "Operations:Punctuality";

    /// <summary>Leaving more than this many seconds before the timetable is early.</summary>
    public int EarlySeconds { get; init; } = 60;

    /// <summary>Leaving up to this many seconds after the timetable is still on time.</summary>
    public int LateSeconds { get; init; } = 180;

    /// <summary>Beyond this it is very late.</summary>
    public int VeryLateSeconds { get; init; } = 300;

    /// <summary>Stops with fewer departures are left out of the stop ranking.</summary>
    public int MinDeparturesPerStop { get; init; } = 20;
}

public enum Punctuality { Early, OnTime, Late, VeryLate }

/// <summary>
/// How punctual departures are, and how many passengers a delay affects. A departure is a stop visit
/// with a departure time, judged by the delay the vehicle logged when leaving (negative = early).
///
/// Punctuality counts every trip except depot runs: lateness doesn't depend on the counting units.
/// Passenger-weighted figures (passengers on board × delay) use only valid trips, whose counts can be
/// trusted; the load on board is the running sum of boardings minus alightings from the trip's start.
/// </summary>
public sealed class PunctualityReport(AppDbContext db, IOptions<PunctualityRules> options, HybridCache cache, StopDirections stopDirections, DayCalendar calendar)
{
    [ImmutableObject(true)]
    private sealed record Cached(PunctualityReportDto Report);

    public async Task<PunctualityReportDto> GetAsync(int? line, ReportPeriod period = default, TimesSource times = TimesSource.VehicleLog, CancellationToken ct = default)
    {
        var version = $"{await db.SourceFiles.MaxAsync(f => (long?)f.Id, ct) ?? 0}.{await db.Trips.MaxAsync(t => (long?)t.Id, ct) ?? 0}.{await db.RecordedCalls.MaxAsync(c => (long?)c.Id, ct) ?? 0}";
        var key = $"operations/punctuality/{version}/{times}/{line?.ToString() ?? "all"}/{period.Key}";
        return (await cache.GetOrCreateAsync(key, async token => new Cached(await BuildAsync(line, period, times, token)), cancellationToken: ct)).Report;
    }

    public async Task<PunctualityReportDto> BuildAsync(int? line, ReportPeriod period = default, TimesSource times = TimesSource.VehicleLog, CancellationToken ct = default)
    {
        var rules = options.Value;
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        var stops = (await db.Stops.AsNoTracking().Select(s => new { s.Code, s.Name, s.Latitude, s.Longitude }).ToListAsync(ct))
            .ToDictionary(s => s.Code, s => new StopInfo(s.Name, s.Latitude, s.Longitude));
        var (departures, lines, days) = times == TimesSource.Transportella
            ? await TransportellaDeparturesAsync(line, period, rules, stops, ct)
            : await VehicleLogDeparturesAsync(line, period, rules, ct);
        var directions = await stopDirections.GetAsync(ct);

        PunctualitySummaryDto Summarize(IEnumerable<Departure> group)
        {
            var list = group.ToList();
            var weighted = list.Where(d => d.Load is not null).ToList();
            var passengers = weighted.Sum(d => (long)d.Load!.Value);
            return new PunctualitySummaryDto(
                list.Count,
                list.Count(d => d.Judgement == Punctuality.Early),
                list.Count(d => d.Judgement == Punctuality.OnTime),
                list.Count(d => d.Judgement == Punctuality.Late),
                list.Count(d => d.Judgement == Punctuality.VeryLate),
                list.Count == 0 ? 0 : Median(list.Select(d => (double)d.Delay).ToList()),
                Math.Round(weighted.Sum(d => (double)d.Load!.Value * Math.Max(0, d.Delay - rules.LateSeconds) / 60), 0),
                passengers == 0 ? null : Math.Round((double)weighted.Where(d => d.Judgement is Punctuality.OnTime).Sum(d => (long)d.Load!.Value) / passengers, 3));
        }

        var byHour = departures.GroupBy(d => d.At.Hour).OrderBy(g => g.Key)
            .Select(g => new PunctualityHourDto(g.Key, Summarize(g))).ToList();
        // By day of week: how many such days the period has, so a reader can tell one Monday from four.
        var byWeekday = departures.GroupBy(d => ReportPeriod.Weekday(d.At)).OrderBy(g => g.Key)
            .Select(g => new PunctualityWeekdayDto(g.Key, g.Select(d => d.At.Date).Distinct().Count(), Summarize(g))).ToList();
        var byWeekHour = departures.GroupBy(d => (Weekday: ReportPeriod.Weekday(d.At), d.At.Hour)).OrderBy(g => g.Key)
            .Select(g => new PunctualityWeekHourDto(g.Key.Weekday, g.Key.Hour, Summarize(g))).ToList();
        var byLine = departures.Where(d => d.Line is not null).GroupBy(d => d.Line!.Value).OrderBy(g => g.Key)
            .Select(g => new PunctualityLineDto(g.Key, Summarize(g))).ToList();
        var byStop = departures.GroupBy(d => d.StopCode)
            .Where(g => g.Count() >= rules.MinDeparturesPerStop)
            .Select(g =>
            {
                var stop = stops.GetValueOrDefault(g.Key);
                var direction = directions.GetValueOrDefault(g.Key);
                return new PunctualityStopDto(g.Key, stop?.Name ?? "", stop?.Latitude, stop?.Longitude, direction?.Toward, direction?.Bearing, Summarize(g));
            })
            .OrderByDescending(s => s.Summary.Late + s.Summary.VeryLate)
            .ToList();

        return new PunctualityReportDto(
            departures.Count == 0 ? null : DateOnly.FromDateTime(departures.Min(d => d.At)),
            departures.Count == 0 ? null : DateOnly.FromDateTime(departures.Max(d => d.At)),
            line, lines, rules, Summarize(departures), byHour, byWeekday, byWeekHour, byLine, byStop, days,
            times, HasPassengers: times == TimesSource.VehicleLog);
    }

    /// <summary>
    /// Departures as Transportella recorded them: planned and actual times, no passengers. Its stops are
    /// stations (the post isn't recorded), keyed station × 100. A call counts only where its stop name
    /// matches the operator's stop list for that station, so another network's calls stay out.
    /// </summary>
    private async Task<(List<Departure> Departures, List<int> Lines, IReadOnlyList<DateOnly> Days)> TransportellaDeparturesAsync(
        int? line, ReportPeriod period, PunctualityRules rules, Dictionary<int, StopInfo> stops, CancellationToken ct)
    {
        // Each station's post names (folded), and a station-level stop: the posts' name, their average position.
        var stations = stops.GroupBy(s => s.Key / 100).ToList();
        var stationNames = stations.ToDictionary(g => g.Key, g => g.Select(s => EpcompStationsImporter.Fold(s.Value.Name)).Where(n => n.Length > 0).ToHashSet());
        foreach (var station in stations)
        {
            var placed = station.Where(s => s.Value.Latitude is not null && s.Value.Longitude is not null).ToList();
            stops.TryAdd(station.Key * 100, new StopInfo(
                station.First().Value.Name,
                placed.Count == 0 ? null : placed.Average(s => s.Value.Latitude!.Value),
                placed.Count == 0 ? null : placed.Average(s => s.Value.Longitude!.Value)));
        }

        var calls = db.RecordedCalls.AsNoTracking().Where(c => c.StationId > 0 && c.StopName != null);
        if (line is { } l)
        {
            var lineText = l.ToString(CultureInfo.InvariantCulture);
            calls = calls.Where(c => c.Line == lineText);
        }
        if (period.Start is { } start)
        {
            calls = calls.Where(c => c.TripStart >= start);
        }
        if (period.End is { } end)
        {
            calls = calls.Where(c => c.TripStart < end);
        }

        var folded = new Dictionary<string, string>();
        var departures = new List<Departure>();
        var lines = new HashSet<int>();
        var days = new HashSet<DateOnly>();
        await foreach (var c in calls
                           .Select(c => new { c.StationId, c.StopName, c.Line, c.TripStart, c.PlannedArrival, c.PlannedDeparture, c.ActualArrival, c.ActualDeparture })
                           .AsAsyncEnumerable().WithCancellation(ct))
        {
            if (!folded.TryGetValue(c.StopName!, out var name))
            {
                folded[c.StopName!] = name = EpcompStationsImporter.Fold(c.StopName!);
            }
            if (!stationNames.TryGetValue(c.StationId, out var names) || !names.Contains(name))
            {
                continue;
            }
            days.Add(DateOnly.FromDateTime(c.TripStart));
            var lineNumber = int.TryParse(c.Line, CultureInfo.InvariantCulture, out var number) ? number : (int?)null;
            if (lineNumber is { } n)
            {
                lines.Add(n);
            }
            if (!calendar.Keeps(period.Days, c.TripStart))
            {
                continue;
            }
            // The departure where there is one; at a trip's last stop, the arrival.
            var (planned, actual) = c.PlannedDeparture is not null && c.ActualDeparture is not null
                ? (c.PlannedDeparture, c.ActualDeparture)
                : (c.PlannedArrival, c.ActualArrival);
            if (planned is not { } p || actual is not { } a)
            {
                continue;
            }
            var delay = (int)Math.Round((a - p).TotalSeconds);
            departures.Add(new Departure(c.StationId * 100, lineNumber, a, delay, null, Judge(delay, rules)));
        }
        return (departures, lines.Order().ToList(), days.Order().ToList());
    }

    /// <summary>Departures as the vehicles logged them, with the load on board from their counting units.</summary>
    private async Task<(List<Departure> Departures, List<int> Lines, IReadOnlyList<DateOnly> Days)> VehicleLogDeparturesAsync(
        int? line, ReportPeriod period, PunctualityRules rules, CancellationToken ct)
    {
        var query = db.StopVisits.AsNoTracking().Where(v => v.Trip.SourceFileId != null && !v.Trip.IsDepotRun);
        if (line is { } l)
        {
            query = query.Where(v => v.Trip.Pattern != null && v.Trip.Pattern.LineId == l);
        }
        if (period.Start is { } start)
        {
            query = query.Where(v => v.Trip.StartTime >= start);
        }
        if (period.End is { } end)
        {
            query = query.Where(v => v.Trip.StartTime < end);
        }
        var rows = await query
            .Select(v => new
            {
                v.TripId, v.Sequence, v.StopCode, v.DepartureTime, v.DelaySeconds, v.Boardings, v.Alightings, v.IsPassThrough,
                v.Trip.IsValid, v.Trip.StartTime,
                Line = v.Trip.Pattern != null ? (int?)v.Trip.Pattern.LineId : null,
            })
            .ToListAsync(ct);
        rows = rows.Where(r => calendar.Keeps(period.Days, r.StartTime)).ToList();

        // Load on board after each stop: running sum over the trip, never below zero (drift).
        var departures = new List<Departure>();
        foreach (var trip in rows.GroupBy(r => r.TripId))
        {
            var load = 0;
            foreach (var r in trip.OrderBy(r => r.Sequence))
            {
                load = Math.Max(0, load + r.Boardings - r.Alightings);
                if (r.DepartureTime is { } at && !r.IsPassThrough)
                {
                    departures.Add(new Departure(r.StopCode, r.Line, at, r.DelaySeconds, r.IsValid ? load : null, Judge(r.DelaySeconds, rules)));
                }
            }
        }

        var lines = await db.Trips.AsNoTracking()
            .Where(t => t.SourceFileId != null && t.Pattern != null)
            .Select(t => t.Pattern!.LineId).Distinct().OrderBy(x => x).ToListAsync(ct);
        return (departures, lines, await ReportPeriod.DaysWithDataAsync(db, ct));
    }

    private static Punctuality Judge(int delay, PunctualityRules rules) =>
        delay < -rules.EarlySeconds ? Punctuality.Early
        : delay <= rules.LateSeconds ? Punctuality.OnTime
        : delay <= rules.VeryLateSeconds ? Punctuality.Late
        : Punctuality.VeryLate;

    private static double Median(List<double> values)
    {
        values.Sort();
        var mid = values.Count / 2;
        return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2;
    }

    /// <param name="Load">Passengers on board when leaving; null on invalid trips (counts not trusted).</param>
    private sealed record StopInfo(string Name, double? Latitude, double? Longitude);

    private sealed record Departure(int StopCode, int? Line, DateTime At, int Delay, int? Load, Punctuality Judgement);
}

/// <param name="MedianDelaySeconds">Negative = early.</param>
/// <param name="PassengerMinutesLate">Passengers on board × minutes beyond the on-time limit, summed.</param>
/// <param name="PassengersOnTimeShare">Share of passengers on board who left on time; null without trusted counts.</param>
public sealed record PunctualitySummaryDto(
    int Departures, int Early, int OnTime, int Late, int VeryLate, double MedianDelaySeconds,
    double PassengerMinutesLate, double? PassengersOnTimeShare);

public sealed record PunctualityHourDto(int Hour, PunctualitySummaryDto Summary);

/// <param name="Weekday">1 = Monday … 7 = Sunday.</param>
/// <param name="Days">How many of these weekdays the period has with departures.</param>
public sealed record PunctualityWeekdayDto(int Weekday, int Days, PunctualitySummaryDto Summary);

public sealed record PunctualityWeekHourDto(int Weekday, int Hour, PunctualitySummaryDto Summary);

public sealed record PunctualityLineDto(int Line, PunctualitySummaryDto Summary);

public sealed record PunctualityStopDto(
    int Code, string Name, double? Latitude, double? Longitude, string? Toward, double? Bearing, PunctualitySummaryDto Summary);

public sealed record PunctualityReportDto(
    DateOnly? From, DateOnly? To, int? Line, IReadOnlyList<int> Lines, PunctualityRules Rules,
    PunctualitySummaryDto Total, IReadOnlyList<PunctualityHourDto> Hours, IReadOnlyList<PunctualityWeekdayDto> Weekdays,
    IReadOnlyList<PunctualityWeekHourDto> WeekHours, IReadOnlyList<PunctualityLineDto> ByLine,
    IReadOnlyList<PunctualityStopDto> Stops, IReadOnlyList<DateOnly> Days, TimesSource Times, bool HasPassengers);
