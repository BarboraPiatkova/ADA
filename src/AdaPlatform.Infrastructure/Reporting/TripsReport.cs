using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// Trips as ADA's "Jízdy" listed them: every trip of the period, invalid ones and depot runs included and
/// marked, narrowed by line and vehicle; and one trip stop by stop, with the doors that flagged their
/// counts, the vehicle's known faults during it and ADA's overview (kilometres, vehicle and place
/// kilometres, passengers carried). Only trips from the vehicle logs, as on every other screen.
/// </summary>
public sealed class TripsReport(AppDbContext db, DayCalendar calendar)
{
    /// <summary>More trips than this are cut off (the newest are dropped); the screen says so.</summary>
    public const int MaxListed = 20_000;

    public async Task<TripListDto> GetAsync(int? line, int? vehicle, ReportPeriod period = default, CancellationToken ct = default)
    {
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));
        var trips = db.Trips.AsNoTracking().Where(t => t.SourceFileId != null);
        if (line is { } l)
        {
            trips = trips.Where(t => t.Pattern != null && t.Pattern.LineId == l);
        }
        if (vehicle is { } v)
        {
            trips = trips.Where(t => t.VehicleId == v || t.SecondVehicleId == v);
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
            .OrderBy(t => t.StartTime).ThenBy(t => t.VehicleId)
            .Select(t => new
            {
                t.Id,
                t.VehicleId,
                t.SecondVehicleId,
                t.StartTime,
                t.EndTime,
                Line = t.Pattern != null ? (int?)t.Pattern.LineId : null,
                t.PatternCode,
                t.BlockCode,
                First = t.Pattern != null ? t.Pattern.FirstStopName : null,
                Last = t.Pattern != null ? t.Pattern.LastStopName : null,
                Stops = t.StopVisits.Count(s => !s.IsPassThrough),
                t.Boardings,
                t.Alightings,
                t.IsValid,
                t.IsDepotRun,
                FlaggedStops = t.StopVisits.Count(s => s.DoorCounts.Any(d => d.IsFlaggedInvalid)),
            })
            .ToListAsync(ct);
        var kept = rows.Where(t => calendar.Keeps(period.Days, t.StartTime)).ToList();

        var lines = await db.Trips.AsNoTracking()
            .Where(t => t.SourceFileId != null && t.Pattern != null)
            .Select(t => t.Pattern!.LineId).Distinct().OrderBy(x => x).ToListAsync(ct);
        var vehicles = await db.Trips.AsNoTracking()
            .Where(t => t.SourceFileId != null)
            .Select(t => t.VehicleId).Distinct().OrderBy(x => x).ToListAsync(ct);

        return new TripListDto(
            kept.Count == 0 ? null : DateOnly.FromDateTime(kept[0].StartTime),
            kept.Count == 0 ? null : DateOnly.FromDateTime(kept[^1].StartTime),
            line, vehicle, lines, vehicles, kept.Count,
            kept.Take(MaxListed).Select(t => new TripRowDto(
                t.Id, t.VehicleId, t.SecondVehicleId, t.StartTime, t.EndTime, t.Line, t.PatternCode, t.BlockCode,
                Clean(t.First), Clean(t.Last), t.Stops, t.Boardings, t.Alightings, t.IsValid, t.IsDepotRun, t.FlaggedStops)).ToList(),
            await ReportPeriod.DaysWithDataAsync(db, ct));
    }

    /// <summary>One trip stop by stop; null if there's no such trip from the vehicle logs.</summary>
    public async Task<TripDetailDto?> GetTripAsync(long id, CancellationToken ct = default)
    {
        var trip = await db.Trips.AsNoTracking()
            .Where(t => t.Id == id && t.SourceFileId != null)
            .Select(t => new
            {
                t.Id,
                t.VehicleId,
                t.SecondVehicleId,
                t.StartTime,
                t.EndTime,
                t.IsValid,
                t.IsDepotRun,
                t.PatternCode,
                t.BlockCode,
                Line = t.Pattern != null ? (int?)t.Pattern.LineId : null,
                First = t.Pattern != null ? t.Pattern.FirstStopName : null,
                Last = t.Pattern != null ? t.Pattern.LastStopName : null,
                LengthKm = t.Pattern != null ? t.Pattern.LengthKm : null,
                Capacity = t.Vehicle.SeatingCapacity + t.Vehicle.StandingCapacity,
                Visits = t.StopVisits.OrderBy(v => v.Sequence).Select(v => new
                {
                    v.Sequence,
                    v.StopCode,
                    v.ArrivalTime,
                    v.DepartureTime,
                    v.Boardings,
                    v.Alightings,
                    v.Occupancy,
                    v.DelaySeconds,
                    v.IsPassThrough,
                    Flagged = v.DoorCounts.Where(d => d.IsFlaggedInvalid).Select(d => d.CountingDevice.DeviceNumber).OrderBy(n => n).ToList(),
                }).ToList(),
            })
            .SingleOrDefaultAsync(ct);
        if (trip is null)
        {
            return null;
        }

        var codes = trip.Visits.Select(v => v.StopCode).Distinct().ToList();
        var names = await db.Stops.AsNoTracking().Where(s => codes.Contains(s.Code)).ToDictionaryAsync(s => s.Code, s => s.Name, ct);
        // Faults recorded against this trip, or of the vehicle and overlapping it.
        var faults = await db.DeviceFaults.AsNoTracking()
            .Where(f => f.TripId == id || (f.VehicleId == trip.VehicleId && f.From < trip.EndTime && (f.To ?? f.From) >= trip.StartTime))
            .OrderBy(f => f.From)
            .Select(f => new VehicleFaultDto(f.Id, f.DeviceNumber, f.TripId, f.From, f.To, f.Kind.ToString(), f.Source.ToString(), f.Details))
            .ToListAsync(ct);

        var stops = trip.Visits.Select(v => new VehicleStopDto(
            v.Sequence, v.StopCode, names.GetValueOrDefault(v.StopCode) ?? "", v.ArrivalTime, v.DepartureTime,
            v.ArrivalTime is { } a && v.DepartureTime is { } d ? (int)(d - a).TotalSeconds : null,
            v.Boardings, v.Alightings, v.Occupancy, v.DelaySeconds, v.IsPassThrough)).ToList();
        var passengers = trip.Visits.Sum(v => v.Boardings);
        var vehicleKm = trip.LengthKm * (trip.SecondVehicleId is null ? 1 : 2);
        return new TripDetailDto(
            new VehicleTripDto(trip.Id, trip.StartTime, trip.EndTime, trip.Line, trip.PatternCode, Clean(trip.First), Clean(trip.Last), trip.IsValid, trip.IsDepotRun, stops),
            trip.VehicleId, trip.SecondVehicleId, trip.BlockCode, trip.Capacity,
            new TripOverviewDto(trip.LengthKm, vehicleKm, trip.Capacity is > 0 ? trip.LengthKm * trip.Capacity : null, passengers),
            trip.Visits.Where(v => v.Flagged.Count > 0).Select(v => new FlaggedStopDto(v.Sequence, v.StopCode, v.Flagged)).ToList(),
            faults);
    }

    // ADA writes terminus names with the stop code in front ("14901 Purmerendská").
    private static string? Clean(string? name) => name is null ? null : System.Text.RegularExpressions.Regex.Replace(name, @"^\d+\s+", "");
}

/// <param name="SecondVehicleId">The second unit of a coupled set, if any.</param>
/// <param name="Stops">Stops served (pass-throughs not counted).</param>
/// <param name="FlaggedStops">Stops where a counting unit flagged its count as invalid (why a trip is invalid).</param>
public sealed record TripRowDto(
    long Id, int VehicleId, int? SecondVehicleId, DateTime Start, DateTime End, int? Line, int? PatternCode, string? Block,
    string? FirstStopName, string? LastStopName, int Stops, int Boardings, int Alightings, bool IsValid, bool IsDepotRun, int FlaggedStops);

/// <param name="Total">Trips matching the filter; more than <see cref="TripsReport.MaxListed"/> are not all listed.</param>
/// <param name="Lines">Every line with trips, for the filter.</param>
/// <param name="Vehicles">Every vehicle with trips, for the filter.</param>
/// <param name="Days">Every day with a trip from the vehicle logs, for the period picker.</param>
public sealed record TripListDto(
    DateOnly? From, DateOnly? To, int? Line, int? Vehicle, IReadOnlyList<int> Lines, IReadOnlyList<int> Vehicles, int Total,
    IReadOnlyList<TripRowDto> Trips, IReadOnlyList<DateOnly> Days);

/// <summary>ADA's "Přehled" of a trip.</summary>
/// <param name="Km">The pattern's length; null when the timetable doesn't give it.</param>
/// <param name="VehicleKm">Kilometres times the units in the set (two when coupled).</param>
/// <param name="PlaceKm">Kilometres times the vehicle's capacity; null when the capacity is unknown.</param>
/// <param name="Passengers">Passengers carried: everyone who boarded.</param>
public sealed record TripOverviewDto(double? Km, double? VehicleKm, double? PlaceKm, int Passengers);

/// <param name="DeviceNumbers">The counting units that flagged their count at this stop.</param>
public sealed record FlaggedStopDto(int Sequence, int StopCode, IReadOnlyList<int> DeviceNumbers);

public sealed record TripDetailDto(
    VehicleTripDto Trip, int VehicleId, int? SecondVehicleId, string? Block, int? Capacity, TripOverviewDto Overview,
    IReadOnlyList<FlaggedStopDto> FlaggedStops, IReadOnlyList<VehicleFaultDto> Faults);
