using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// Stops as ADA's "Zastávky" summed them: per stop post, who boarded and alighted and how many were on
/// board after it, over the trips of a period, narrowed by line and vehicle; and one stop by line, hour and
/// day of week. By default only the trips the statistics use (valid, no depot runs); ADA's "Zobrazit jenom
/// validní" unticked gives every trip. Only trips from the vehicle logs, as on every other screen.
///
/// The load on board is the running sum of boardings minus alightings from the trip's start, floored at
/// zero, as on the occupancy screen.
/// </summary>
public sealed class StopStatisticsReport(AppDbContext db, DayCalendar calendar, StopDirections directions)
{
    public async Task<StopStatisticsDto> GetAsync(StopFilter filter, CancellationToken ct = default)
    {
        var visits = await VisitsAsync(filter, null, ct);
        var names = await db.Stops.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Name, ct);
        var toward = await directions.GetAsync(ct);
        var lines = await db.Trips.AsNoTracking()
            .Where(t => t.SourceFileId != null && t.Pattern != null)
            .Select(t => t.Pattern!.LineId).Distinct().OrderBy(x => x).ToListAsync(ct);
        var vehicles = await db.Trips.AsNoTracking()
            .Where(t => t.SourceFileId != null)
            .Select(t => t.VehicleId).Distinct().OrderBy(x => x).ToListAsync(ct);

        var stops = visits.GroupBy(v => v.StopCode)
            .Select(g =>
            {
                var served = g.Where(v => !v.IsPassThrough).ToList();
                return new StopStatDto(
                    g.Key, names.GetValueOrDefault(g.Key) ?? "", toward.GetValueOrDefault(g.Key)?.Toward,
                    served.Count, g.Count() - served.Count, served.Sum(v => v.Boardings), served.Sum(v => v.Alightings),
                    served.Count == 0 ? 0 : Math.Round(served.Average(v => v.Load), 1), served.Count == 0 ? 0 : served.Max(v => v.Load),
                    g.Where(v => v.Line is not null).Select(v => v.Line!.Value).Distinct().Order().ToList());
            })
            .OrderBy(s => s.Code)
            .ToList();

        return new StopStatisticsDto(
            visits.Count == 0 ? null : DateOnly.FromDateTime(visits.Min(v => v.TripStart)),
            visits.Count == 0 ? null : DateOnly.FromDateTime(visits.Max(v => v.TripStart)),
            filter.Line, filter.Vehicle, filter.AllTrips, lines, vehicles, visits.Select(v => v.TripId).Distinct().Count(), stops,
            await ReportPeriod.DaysWithDataAsync(db, ct));
    }

    /// <summary>One stop post: its figures by line, by hour of arrival and by day of week; null if there's no such stop.</summary>
    public async Task<StopStatisticsDetailDto?> GetStopAsync(int code, StopFilter filter, CancellationToken ct = default)
    {
        var stop = await db.Stops.AsNoTracking().Where(s => s.Code == code).Select(s => new { s.Code, s.Name }).SingleOrDefaultAsync(ct);
        if (stop is null)
        {
            return null;
        }
        var served = (await VisitsAsync(filter, code, ct)).Where(v => !v.IsPassThrough).ToList();
        var toward = (await directions.GetAsync(ct)).GetValueOrDefault(code)?.Toward;

        var byLine = served.GroupBy(v => v.Line).OrderBy(g => g.Key ?? int.MaxValue)
            .Select(g => new StopLineStatDto(g.Key, g.Count(), g.Sum(v => v.Boardings), g.Sum(v => v.Alightings), Math.Round(g.Average(v => v.Load), 1)))
            .ToList();
        var byHour = served.Where(v => v.Arrival is not null).GroupBy(v => v.Arrival!.Value.Hour).OrderBy(g => g.Key)
            .Select(g => new StopHourStatDto(g.Key, g.Count(), g.Sum(v => v.Boardings), g.Sum(v => v.Alightings)))
            .ToList();
        // Per day of week, with how many such days there were: the screen shows figures per day.
        var byWeekday = served.GroupBy(v => ReportPeriod.Weekday(v.TripStart)).OrderBy(g => g.Key)
            .Select(g => new StopWeekdayStatDto(g.Key, g.Select(v => v.TripStart.Date).Distinct().Count(), g.Sum(v => v.Boardings), g.Sum(v => v.Alightings)))
            .ToList();
        return new StopStatisticsDetailDto(stop.Code, stop.Name, toward, byLine, byHour, byWeekday);
    }

    /// <summary>Every stop visit of the filtered trips, with the load on board after it.</summary>
    private async Task<List<LoadedVisit>> VisitsAsync(StopFilter filter, int? onlyStop, CancellationToken ct)
    {
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        var trips = db.Trips.AsNoTracking().Where(t => t.SourceFileId != null);
        if (!filter.AllTrips)
        {
            trips = trips.Where(t => t.IsValid && !t.IsDepotRun);
        }
        if (filter.Line is { } l)
        {
            trips = trips.Where(t => t.Pattern != null && t.Pattern.LineId == l);
        }
        if (filter.Vehicle is { } v)
        {
            trips = trips.Where(t => t.VehicleId == v || t.SecondVehicleId == v);
        }
        if (filter.Period.Start is { } start)
        {
            trips = trips.Where(t => t.StartTime >= start);
        }
        if (filter.Period.End is { } end)
        {
            trips = trips.Where(t => t.StartTime < end);
        }
        if (onlyStop is { } code)
        {
            // The load needs the whole trip, so narrow to the trips calling there, not to the visits.
            trips = trips.Where(t => t.StopVisits.Any(s => s.StopCode == code));
        }

        var rows = await trips
            .Select(t => new
            {
                t.Id,
                t.StartTime,
                Line = t.Pattern != null ? (int?)t.Pattern.LineId : null,
                Visits = t.StopVisits.OrderBy(s => s.Sequence)
                    .Select(s => new { s.StopCode, s.ArrivalTime, s.Boardings, s.Alightings, s.IsPassThrough }).ToList(),
            })
            .ToListAsync(ct);

        var period = filter.Period;
        return rows.Where(t => calendar.Keeps(period.Days, t.StartTime))
            .SelectMany(t =>
            {
                var load = 0;
                return t.Visits.Select(s =>
                {
                    load = Math.Max(0, load + s.Boardings - s.Alightings);
                    return new LoadedVisit(t.Id, t.StartTime, t.Line, s.StopCode, s.ArrivalTime, s.Boardings, s.Alightings, load, s.IsPassThrough);
                }).ToList();
            })
            .Where(v => onlyStop is null || v.StopCode == onlyStop)
            .ToList();
    }

    private sealed record LoadedVisit(long TripId, DateTime TripStart, int? Line, int StopCode, DateTime? Arrival, int Boardings, int Alightings, int Load, bool IsPassThrough);
}

/// <summary>Which trips the stop statistics count.</summary>
/// <param name="AllTrips">Every trip; otherwise only valid trips without depot runs, as the statistics.</param>
public readonly record struct StopFilter(int? Line, int? Vehicle, bool AllTrips, ReportPeriod Period);

/// <param name="Toward">The direction the post serves (its most frequent destination).</param>
/// <param name="Visits">Calls where the vehicle stopped.</param>
/// <param name="PassThroughs">Calls where it went through without stopping.</param>
/// <param name="MeanLoad">Mean passengers on board after the stop.</param>
/// <param name="Lines">The lines calling here.</param>
public sealed record StopStatDto(
    int Code, string Name, string? Toward, int Visits, int PassThroughs, int Boardings, int Alightings, double MeanLoad, int MaxLoad, IReadOnlyList<int> Lines);

/// <param name="AllTrips">Whether every trip counts, or only the valid ones without depot runs.</param>
/// <param name="Trips">Trips counted.</param>
public sealed record StopStatisticsDto(
    DateOnly? From, DateOnly? To, int? Line, int? Vehicle, bool AllTrips, IReadOnlyList<int> Lines, IReadOnlyList<int> Vehicles, int Trips,
    IReadOnlyList<StopStatDto> Stops, IReadOnlyList<DateOnly> Days);

public sealed record StopLineStatDto(int? Line, int Visits, int Boardings, int Alightings, double MeanLoad);

public sealed record StopHourStatDto(int Hour, int Visits, int Boardings, int Alightings);

/// <param name="Weekday">1 = Monday … 7 = Sunday.</param>
/// <param name="Days">How many of these weekdays the period has with calls here.</param>
public sealed record StopWeekdayStatDto(int Weekday, int Days, int Boardings, int Alightings);

public sealed record StopStatisticsDetailDto(
    int Code, string Name, string? Toward, IReadOnlyList<StopLineStatDto> ByLine, IReadOnlyList<StopHourStatDto> ByHour, IReadOnlyList<StopWeekdayStatDto> ByWeekday);
