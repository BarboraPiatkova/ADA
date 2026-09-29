using System.ComponentModel;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// Rules for judging a dwell ("Operations:Dwell"). Provisional, like the health thresholds; a
/// deployment overrides them.
/// </summary>
public sealed record DwellRules
{
    public const string SectionName = "Operations:Dwell";

    /// <summary>A stop shorter than this is never "unexplained".</summary>
    public int MinUnexplainedSeconds { get; init; } = 60;

    /// <summary>How much longer than the passenger exchange explains a stop must be to be listed.</summary>
    public int MinExcessSeconds { get; init; } = 45;

    /// <summary>The line is fitted on stops up to this long, so holds for the timetable don't bend it.</summary>
    public int FitMaxSeconds { get; init; } = 180;

    /// <summary>Stops with fewer visits are left out of the stop ranking (a median of 3 says little).</summary>
    public int MinVisitsPerStop { get; init; } = 20;

    /// <summary>Departure within this many seconds of the timetable: the vehicle probably waited for its time.</summary>
    public int OnTimeSeconds { get; init; } = 60;

    /// <summary>
    /// A long dwell that overlaps in time with long dwells of at least this many other vehicles points
    /// to something stopping traffic (a blockage, an accident, a diversion), not to one vehicle.
    /// </summary>
    public int MinOtherVehiclesAtOnce { get; init; } = 2;
}

/// <summary>
/// How long vehicles stand at stops, and how much of it the passengers explain. Dwell is departure
/// minus arrival of a stop visit; passengers are boardings plus alightings over all doors.
///
/// Only trustworthy visits are used: valid trips (every door counted, no device flagged), not depot
/// runs, doors opened, both times known, and neither the first nor the last stop of the trip (the
/// vehicle waits there for its departure, which isn't passenger exchange).
///
/// Times come from the vehicles' own logs today. When Transportella's records for the same operator
/// exist (<c>RecordedCalls</c>), they can supply the times instead; the report says which it used.
/// </summary>
public sealed class StopDwellReport(AppDbContext db, IOptions<DwellRules> options, HybridCache cache, StopDirections stopDirections, DayCalendar calendar)
{
    public static readonly (int Min, int? Max)[] Bands = [(0, 0), (1, 2), (3, 5), (6, 10), (11, 20), (21, null)];
    // All of them for a normal operator; the cap only keeps a broken data set from flooding the page.
    private const int MaxUnexplainedListed = 5000;

    [ImmutableObject(true)]
    private sealed record Cached(DwellReportDto Report);

    public async Task<DwellReportDto> GetAsync(int? line, ReportPeriod period = default, CancellationToken ct = default)
    {
        // Reconstruction replaces trips without a new import, so the trips are part of the version.
        var version = $"{await db.SourceFiles.MaxAsync(f => (long?)f.Id, ct) ?? 0}.{await db.Trips.MaxAsync(t => (long?)t.Id, ct) ?? 0}";
        var key = $"operations/dwell/{version}/{line?.ToString() ?? "all"}/{period.Key}";
        return (await cache.GetOrCreateAsync(key, async token => new Cached(await BuildAsync(line, period, token)), cancellationToken: ct)).Report;
    }

    public async Task<DwellReportDto> BuildAsync(int? line, ReportPeriod period = default, CancellationToken ct = default)
    {
        var rules = options.Value;
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        var visits = await LoadVisitsAsync(line, stopCode: null, period, ct);
        var days = await ReportPeriod.DaysWithDataAsync(db, ct);

        var lines = await db.Trips.AsNoTracking()
            .Where(t => t.SourceFileId != null && t.Pattern != null)
            .Select(t => t.Pattern!.LineId).Distinct().OrderBy(x => x).ToListAsync(ct);
        var stops = await db.Stops.AsNoTracking()
            .Select(s => new { s.Code, s.Name, s.Latitude, s.Longitude })
            .ToDictionaryAsync(s => s.Code, ct);

        var directions = await stopDirections.GetAsync(ct);
        var model = Fit(visits.Where(v => v.Dwell <= rules.FitMaxSeconds).ToList());
        int Expected(int passengers) => (int)Math.Round(model.BaseSeconds + model.SecondsPerPassenger * passengers);

        var bands = Bands.Select(b =>
        {
            var dwell = visits.Where(v => v.Passengers >= b.Min && (b.Max is null || v.Passengers <= b.Max)).Select(v => (double)v.Dwell).ToList();
            return new DwellBandDto(b.Min, b.Max, dwell.Count, Percentile(dwell, 0.5), Percentile(dwell, 0.9));
        }).ToList();

        bool IsUnexplained(Visit v) => v.Dwell >= rules.MinUnexplainedSeconds && v.Dwell - Expected(v.Passengers) >= rules.MinExcessSeconds;

        var stopRows = visits.GroupBy(v => v.StopCode)
            .Where(g => g.Count() >= rules.MinVisitsPerStop)
            .Select(g =>
            {
                var stop = stops.GetValueOrDefault(g.Key);
                var dwell = g.Select(v => (double)v.Dwell).ToList();
                var direction = directions.GetValueOrDefault(g.Key);
                return new StopDwellDto(
                    g.Key, stop?.Name ?? "", stop?.Latitude, stop?.Longitude,
                    direction?.Toward, direction?.Bearing,
                    g.Count(),
                    Percentile(dwell, 0.5), Percentile(dwell, 0.9),
                    Math.Round(g.Average(v => v.Passengers), 1),
                    Percentile(g.Select(v => (double)(v.Dwell - Expected(v.Passengers))).ToList(), 0.5),
                    g.Count(IsUnexplained));
            })
            .OrderByDescending(s => s.MedianExcessSeconds)
            .ToList();

        var longDwells = visits.Where(IsUnexplained).ToList();
        var othersAtOnce = OtherVehiclesAtOnce(longDwells);
        var unexplained = longDwells
            .OrderByDescending(v => v.Dwell - Expected(v.Passengers))
            .Take(MaxUnexplainedListed)
            .Select(v =>
            {
                var others = othersAtOnce[v];
                var cause = Math.Abs(v.DelaySeconds) <= rules.OnTimeSeconds ? DwellCause.HeldForTimetable
                    : others >= rules.MinOtherVehiclesAtOnce ? DwellCause.SeveralVehicles
                    : DwellCause.Other;
                return new UnexplainedDwellDto(
                    v.Arrival, v.VehicleId, v.Line, v.StopCode, stops.GetValueOrDefault(v.StopCode)?.Name ?? "",
                    v.Dwell, v.Passengers, Expected(v.Passengers), v.DelaySeconds, cause, others);
            })
            .ToList();

        return new DwellReportDto(
            visits.Count == 0 ? null : DateOnly.FromDateTime(visits.Min(v => v.Arrival)),
            visits.Count == 0 ? null : DateOnly.FromDateTime(visits.Max(v => v.Arrival)),
            line, lines, DwellTimeSource.VehicleLog, rules,
            model, bands, stopRows, visits.Count(IsUnexplained), unexplained, days);
    }

    /// <summary>
    /// The trustworthy visits (see the class summary) of trips starting in the period, optionally of one
    /// line and one stop. The last stop is judged per trip, so a stop filter doesn't change which visits count.
    /// </summary>
    private async Task<List<Visit>> LoadVisitsAsync(int? line, int? stopCode, ReportPeriod period, CancellationToken ct)
    {
        var query = db.StopVisits.AsNoTracking()
            .Where(v => v.Trip.SourceFileId != null && v.Trip.IsValid && !v.Trip.IsDepotRun
                        && !v.IsPassThrough && v.Sequence > 1 && v.ArrivalTime != null && v.DepartureTime != null
                        && v.Sequence < v.Trip.StopVisits.Max(s => s.Sequence));
        if (line is { } l)
        {
            query = query.Where(v => v.Trip.Pattern != null && v.Trip.Pattern.LineId == l);
        }
        if (stopCode is { } code)
        {
            query = query.Where(v => v.StopCode == code);
        }
        if (period.Start is { } start)
        {
            query = query.Where(v => v.Trip.StartTime >= start);
        }
        if (period.End is { } end)
        {
            query = query.Where(v => v.Trip.StartTime < end);
        }

        var raw = await query
            .Select(v => new
            {
                v.Trip.StartTime,
                v.Trip.VehicleId,
                Line = v.Trip.Pattern != null ? (int?)v.Trip.Pattern.LineId : null,
                v.StopCode,
                Arrival = v.ArrivalTime!.Value,
                Departure = v.DepartureTime!.Value,
                Passengers = v.Boardings + v.Alightings,
                v.DelaySeconds,
            })
            .ToListAsync(ct);

        return raw
            .Where(v => calendar.Keeps(period.Days, v.StartTime))
            .Select(v => new Visit(v.VehicleId, v.Line, v.StopCode, v.Arrival, (int)(v.Departure - v.Arrival).TotalSeconds, v.Passengers, v.DelaySeconds))
            .Where(v => v.Dwell >= 0 && v.Dwell <= 1800)
            .ToList();
    }

    /// <summary>
    /// Every trustworthy visit of one stop, each with the dwell its passengers explain (the same line
    /// model as the overview), for the stop detail: when the stop is slow, and whether passengers explain it.
    /// </summary>
    public async Task<StopDwellDetailDto> GetStopAsync(int stopCode, int? line, ReportPeriod period = default, CancellationToken ct = default)
    {
        var rules = options.Value;
        var model = (await GetAsync(line, period, ct)).Model;
        var name = await db.Stops.AsNoTracking().Where(s => s.Code == stopCode).Select(s => s.Name).FirstOrDefaultAsync(ct);
        var visits = (await LoadVisitsAsync(line, stopCode, period, ct))
            .OrderBy(v => v.Arrival)
            .Select(v =>
            {
                var expected = (int)Math.Round(model.BaseSeconds + model.SecondsPerPassenger * v.Passengers);
                return new StopVisitDwellDto(v.Arrival, v.VehicleId, v.Line, v.Dwell, v.Passengers, expected, v.DelaySeconds,
                    v.Dwell >= rules.MinUnexplainedSeconds && v.Dwell - expected >= rules.MinExcessSeconds);
            })
            .ToList();
        return new StopDwellDetailDto(stopCode, name ?? "", line, model, visits);
    }

    /// <summary>
    /// One vehicle's trips on one day with every stop, for the trip strip. All trips and stops are
    /// included and marked with their validity, because this view is for finding out what happened.
    /// </summary>
    public async Task<VehicleTripsDayDto> GetVehicleDayAsync(int vehicleId, DateOnly day, CancellationToken ct = default)
    {
        var from = day.ToDateTime(TimeOnly.MinValue);
        var to = from.AddDays(1);
        var starts = await db.Trips.AsNoTracking()
            .Where(t => t.VehicleId == vehicleId && t.SourceFileId != null)
            .Select(t => t.StartTime).ToListAsync(ct);
        var days = starts.Select(DateOnly.FromDateTime).Distinct().Order().ToList();
        var stops = await db.Stops.AsNoTracking().Select(s => new { s.Code, s.Name }).ToDictionaryAsync(s => s.Code, s => s.Name, ct);

        var trips = await db.Trips.AsNoTracking()
            .Where(t => t.VehicleId == vehicleId && t.SourceFileId != null && t.StartTime >= from && t.StartTime < to)
            .OrderBy(t => t.StartTime)
            .Select(t => new
            {
                t.Id, t.StartTime, t.EndTime, t.IsValid, t.IsDepotRun, t.PatternCode,
                Line = t.Pattern != null ? (int?)t.Pattern.LineId : null,
                First = t.Pattern != null ? t.Pattern.FirstStopName : null,
                Last = t.Pattern != null ? t.Pattern.LastStopName : null,
                Visits = t.StopVisits.OrderBy(v => v.Sequence).Select(v => new
                {
                    v.Sequence, v.StopCode, v.ArrivalTime, v.DepartureTime, v.Boardings, v.Alightings, v.Occupancy, v.DelaySeconds, v.IsPassThrough,
                }).ToList(),
            })
            .ToListAsync(ct);

        return new VehicleTripsDayDto(
            vehicleId, day, days,
            trips.Select(t => new VehicleTripDto(
                t.Id, t.StartTime, t.EndTime, t.Line, t.PatternCode, t.First, t.Last, t.IsValid, t.IsDepotRun,
                t.Visits.Select(v => new VehicleStopDto(
                    v.Sequence, v.StopCode, stops.GetValueOrDefault(v.StopCode) ?? "", v.ArrivalTime, v.DepartureTime,
                    v.ArrivalTime is { } a && v.DepartureTime is { } d ? (int)(d - a).TotalSeconds : null,
                    v.Boardings, v.Alightings, v.Occupancy, v.DelaySeconds, v.IsPassThrough)).ToList())).ToList());
    }

    /// <summary>For each long dwell, how many other vehicles were standing long at the same time (their dwells overlap).</summary>
    private static Dictionary<Visit, int> OtherVehiclesAtOnce(List<Visit> longDwells)
    {
        var byArrival = longDwells.OrderBy(v => v.Arrival).ToList();
        var result = new Dictionary<Visit, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < byArrival.Count; i++)
        {
            var v = byArrival[i];
            var end = v.Arrival.AddSeconds(v.Dwell);
            var others = new HashSet<int>();
            // Earlier arrivals still standing when this one arrived, and later ones arriving before it left.
            for (var j = i - 1; j >= 0 && byArrival[j].Arrival >= v.Arrival.AddMinutes(-30); j--)
            {
                if (byArrival[j].Arrival.AddSeconds(byArrival[j].Dwell) > v.Arrival && byArrival[j].VehicleId != v.VehicleId) others.Add(byArrival[j].VehicleId);
            }
            for (var j = i + 1; j < byArrival.Count && byArrival[j].Arrival < end; j++)
            {
                if (byArrival[j].VehicleId != v.VehicleId) others.Add(byArrival[j].VehicleId);
            }
            result[v] = others.Count;
        }
        return result;
    }

    /// <summary>Least-squares line dwell = base + perPassenger × passengers, and the correlation.</summary>
    private static DwellModelDto Fit(List<Visit> visits)
    {
        if (visits.Count < 2)
        {
            return new DwellModelDto(visits.Count, 0, 0, null);
        }
        double n = visits.Count, sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
        foreach (var v in visits)
        {
            sx += v.Passengers; sy += v.Dwell;
            sxx += (double)v.Passengers * v.Passengers; syy += (double)v.Dwell * v.Dwell; sxy += (double)v.Passengers * v.Dwell;
        }
        var cov = sxy - sx * sy / n;
        var varX = sxx - sx * sx / n;
        var varY = syy - sy * sy / n;
        var slope = varX > 0 ? cov / varX : 0;
        var intercept = (sy - slope * sx) / n;
        double? r = varX > 0 && varY > 0 ? cov / Math.Sqrt(varX * varY) : null;
        return new DwellModelDto(visits.Count, Math.Round(intercept, 1), Math.Round(slope, 2), r is { } c ? Math.Round(c, 2) : null);
    }

    private static double Percentile(List<double> values, double p)
    {
        if (values.Count == 0)
        {
            return 0;
        }
        values.Sort();
        var rank = p * (values.Count - 1);
        var low = (int)Math.Floor(rank);
        var high = (int)Math.Ceiling(rank);
        return Math.Round(values[low] + (values[high] - values[low]) * (rank - low), 1);
    }

    private sealed record Visit(int VehicleId, int? Line, int StopCode, DateTime Arrival, int Dwell, int Passengers, int DelaySeconds);
}

public enum DwellTimeSource
{
    /// <summary>Arrival and departure from the vehicle's APC log.</summary>
    VehicleLog,

    /// <summary>Arrival and departure from Transportella's records.</summary>
    Transportella,
}

/// <summary>Likely reason for a long stop the passengers don't explain.</summary>
public enum DwellCause
{
    /// <summary>It left on time: most likely waited for its departure time.</summary>
    HeldForTimetable,

    /// <summary>It left late and other vehicles stood long at the same time: something stopped traffic (a blockage?).</summary>
    SeveralVehicles,

    /// <summary>It left late anyway: something else held it (a wheelchair, a fault, the driver).</summary>
    Other,
}

/// <param name="SecondsPerPassenger">Extra dwell per boarding or alighting passenger.</param>
/// <param name="Correlation">Pearson correlation of dwell and passengers; null with too little data.</param>
public sealed record DwellModelDto(int Visits, double BaseSeconds, double SecondsPerPassenger, double? Correlation);

/// <param name="MaxPassengers">Null for the open top band.</param>
public sealed record DwellBandDto(int MinPassengers, int? MaxPassengers, int Visits, double MedianSeconds, double P90Seconds);

/// <param name="MedianExcessSeconds">Median of dwell minus what its passengers explain: positive = stands longer than its passengers need.</param>
/// <param name="Toward">The most frequent destination of trips calling here: which direction this post serves.</param>
/// <param name="Bearing">Compass direction (0 = north) towards the most frequent next stop; null without positions.</param>
public sealed record StopDwellDto(
    int Code, string Name, double? Latitude, double? Longitude, string? Toward, double? Bearing, int Visits,
    double MedianSeconds, double P90Seconds, double MeanPassengers, double MedianExcessSeconds, int Unexplained);

/// <param name="DelaySeconds">Delay at departure; negative = early.</param>
/// <param name="OtherVehiclesAtOnce">Other vehicles standing long at the same time.</param>
public sealed record UnexplainedDwellDto(
    DateTime Arrival, int VehicleId, int? Line, int StopCode, string StopName,
    int DwellSeconds, int Passengers, int ExpectedSeconds, int DelaySeconds, DwellCause Cause, int OtherVehiclesAtOnce);

public sealed record DwellReportDto(
    DateOnly? From, DateOnly? To, int? Line, IReadOnlyList<int> Lines, DwellTimeSource TimeSource, DwellRules Rules,
    DwellModelDto Model, IReadOnlyList<DwellBandDto> Bands, IReadOnlyList<StopDwellDto> Stops,
    int UnexplainedTotal, IReadOnlyList<UnexplainedDwellDto> Unexplained, IReadOnlyList<DateOnly> Days);

/// <param name="Unexplained">Long and not explained by its passengers (the same rule as the overview list).</param>
public sealed record StopVisitDwellDto(DateTime Arrival, int VehicleId, int? Line, int DwellSeconds, int Passengers, int ExpectedSeconds, int DelaySeconds, bool Unexplained);

public sealed record StopDwellDetailDto(int Code, string Name, int? Line, DwellModelDto Model, IReadOnlyList<StopVisitDwellDto> Visits);

/// <param name="DwellSeconds">Null where the log has no arrival or no departure (a pass, the terminus).</param>
public sealed record VehicleStopDto(
    int Sequence, int StopCode, string StopName, DateTime? Arrival, DateTime? Departure, int? DwellSeconds,
    int Boardings, int Alightings, int Occupancy, int DelaySeconds, bool IsPassThrough);

public sealed record VehicleTripDto(
    long Id, DateTime Start, DateTime End, int? Line, int? PatternCode, string? FirstStopName, string? LastStopName,
    bool IsValid, bool IsDepotRun, IReadOnlyList<VehicleStopDto> Stops);

/// <param name="Days">The days this vehicle has trips on, for the day picker.</param>
public sealed record VehicleTripsDayDto(int VehicleId, DateOnly Day, IReadOnlyList<DateOnly> Days, IReadOnlyList<VehicleTripDto> Trips);
