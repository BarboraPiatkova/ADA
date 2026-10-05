using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// The fleet as a register, as ADA's "Vozidla" view listed it: each vehicle's details, its counting
/// devices, when it last sent data, and what its logs hold for the period, day by day. Unlike the
/// statistics, invalid trips and depot runs are counted (and shown as such): this is what was recorded,
/// not what the statistics use. Only trips from the vehicle logs count, as on every other screen.
/// Cheap aggregates straight from the database, so not cached.
/// </summary>
public sealed class FleetReport(AppDbContext db, DayCalendar calendar)
{
    public async Task<FleetReportDto> GetVehiclesAsync(ReportPeriod period = default, CancellationToken ct = default)
    {
        var vehicles = await db.Vehicles.AsNoTracking().OrderBy(v => v.Id)
            .Select(v => new VehicleRow(
                v.Id, v.Depot, v.Traction, v.Model, v.SeatingCapacity, v.StandingCapacity, v.IsExcluded, v.CountingDevices.Count))
            .ToListAsync(ct);
        var days = await DayRowsAsync(null, period, ct);
        var lastData = await db.SourceFiles.AsNoTracking()
            .GroupBy(f => f.VehicleId)
            .Select(g => new { VehicleId = g.Key, Last = (DateOnly?)g.Max(f => f.ServiceDate) })
            .ToDictionaryAsync(x => x.VehicleId, x => x.Last, ct);
        var faults = (await FaultsAsync(null, period, ct)).GroupBy(f => f.VehicleId).ToDictionary(g => g.Key, g => g.Count());

        var byVehicle = days.GroupBy(d => d.VehicleId).ToDictionary(g => g.Key, g => g.ToList());
        return new FleetReportDto(
            days.Count == 0 ? null : days.Min(d => d.Day),
            days.Count == 0 ? null : days.Max(d => d.Day),
            vehicles.Select(v => Summarize(v, byVehicle.GetValueOrDefault(v.Id) ?? [], lastData.GetValueOrDefault(v.Id), faults.GetValueOrDefault(v.Id))).ToList(),
            await ReportPeriod.DaysWithDataAsync(db, ct));
    }

    /// <summary>One vehicle: its summary, devices, days with trips and known faults in the period; null if there's no such vehicle.</summary>
    public async Task<VehicleDetailDto?> GetVehicleAsync(int vehicleId, ReportPeriod period = default, CancellationToken ct = default)
    {
        var vehicle = await db.Vehicles.AsNoTracking().Where(v => v.Id == vehicleId)
            .Select(v => new VehicleRow(
                v.Id, v.Depot, v.Traction, v.Model, v.SeatingCapacity, v.StandingCapacity, v.IsExcluded, v.CountingDevices.Count))
            .SingleOrDefaultAsync(ct);
        if (vehicle is null)
        {
            return null;
        }
        var devices = await db.CountingDevices.AsNoTracking().Where(d => d.VehicleId == vehicleId).OrderBy(d => d.DeviceNumber)
            .Select(d => new VehicleDeviceDto(d.DeviceNumber, d.FirmwareVersion, d.FirstSeenAt, d.LastSeenAt))
            .ToListAsync(ct);
        var days = await DayRowsAsync(vehicleId, period, ct);
        var lastData = await db.SourceFiles.AsNoTracking().Where(f => f.VehicleId == vehicleId).MaxAsync(f => (DateOnly?)f.ServiceDate, ct);
        var faults = await FaultsAsync(vehicleId, period, ct);

        return new VehicleDetailDto(
            Summarize(vehicle, days, lastData, faults.Count),
            devices,
            days.OrderBy(d => d.Day).Select(d => new VehicleDaySummaryDto(d.Day, d.Trips, d.InvalidTrips, d.DepotRuns, d.Boardings, d.Alightings, d.FirstStart, d.LastEnd)).ToList(),
            faults.Select(f => new VehicleFaultDto(f.Id, f.DeviceNumber, f.TripId, f.From, f.To, f.Kind.ToString(), f.Source.ToString(), f.Details)).ToList(),
            await ReportPeriod.DaysWithDataAsync(db, ct));
    }

    /// <summary>Trip totals per vehicle and day (the day a trip starts on), kept to the period's kind of day.</summary>
    private async Task<List<DayRow>> DayRowsAsync(int? vehicleId, ReportPeriod period, CancellationToken ct)
    {
        var trips = db.Trips.AsNoTracking().Where(t => t.SourceFileId != null);
        if (vehicleId is { } id)
        {
            trips = trips.Where(t => t.VehicleId == id);
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
            .GroupBy(t => new { t.VehicleId, t.StartTime.Date })
            .Select(g => new
            {
                g.Key.VehicleId,
                g.Key.Date,
                Trips = g.Count(),
                Invalid = g.Count(t => !t.IsValid),
                DepotRuns = g.Count(t => t.IsDepotRun),
                Boardings = g.Sum(t => t.Boardings),
                Alightings = g.Sum(t => t.Alightings),
                FirstStart = g.Min(t => t.StartTime),
                LastEnd = g.Max(t => t.EndTime),
            })
            .ToListAsync(ct);
        return rows.Where(r => calendar.Keeps(period.Days, r.Date))
            .Select(r => new DayRow(r.VehicleId, DateOnly.FromDateTime(r.Date), r.Trips, r.Invalid, r.DepotRuns, r.Boardings, r.Alightings, r.FirstStart, r.LastEnd))
            .ToList();
    }

    /// <summary>Known device faults that began in the period, from any source (ADA's records or the detector).</summary>
    private async Task<List<Domain.Quality.DeviceFault>> FaultsAsync(int? vehicleId, ReportPeriod period, CancellationToken ct)
    {
        var faults = db.DeviceFaults.AsNoTracking();
        if (vehicleId is { } id)
        {
            faults = faults.Where(f => f.VehicleId == id);
        }
        if (period.Start is { } start)
        {
            faults = faults.Where(f => f.From >= start);
        }
        if (period.End is { } end)
        {
            faults = faults.Where(f => f.From < end);
        }
        return (await faults.OrderBy(f => f.From).ToListAsync(ct)).Where(f => calendar.Keeps(period.Days, f.From)).ToList();
    }

    private static FleetVehicleDto Summarize(VehicleRow v, List<DayRow> days, DateOnly? lastData, int faults) => new(
        v.Id, v.Depot, v.Traction, v.Model, v.SeatingCapacity, v.StandingCapacity, v.SeatingCapacity + v.StandingCapacity,
        v.IsExcluded, v.Devices, lastData, days.Count, days.Sum(d => d.Trips), days.Sum(d => d.InvalidTrips),
        days.Sum(d => d.Boardings), days.Sum(d => d.Alightings), faults);

    private sealed record VehicleRow(int Id, string? Depot, string? Traction, string? Model, int? SeatingCapacity, int? StandingCapacity, bool IsExcluded, int Devices);

    private sealed record DayRow(int VehicleId, DateOnly Day, int Trips, int InvalidTrips, int DepotRuns, int Boardings, int Alightings, DateTime FirstStart, DateTime LastEnd);
}

/// <param name="Capacity">Seats plus standing places; null unless both are known.</param>
/// <param name="LastData">The last service day a log file of this vehicle was imported for (ADA: "Poslední data").</param>
/// <param name="Days">Days in the period with trips.</param>
/// <param name="Trips">All trips in the period, invalid ones and depot runs included.</param>
/// <param name="Faults">Known device faults that began in the period.</param>
public sealed record FleetVehicleDto(
    int Id, string? Depot, string? Traction, string? Model, int? SeatingCapacity, int? StandingCapacity, int? Capacity,
    bool IsExcluded, int Devices, DateOnly? LastData, int Days, int Trips, int InvalidTrips, int Boardings, int Alightings, int Faults);

/// <param name="Days">Every day with a trip from the vehicle logs, for the period picker.</param>
public sealed record FleetReportDto(DateOnly? From, DateOnly? To, IReadOnlyList<FleetVehicleDto> Vehicles, IReadOnlyList<DateOnly> Days);

public sealed record VehicleDeviceDto(int DeviceNumber, string? FirmwareVersion, DateTime FirstSeen, DateTime LastSeen);

public sealed record VehicleDaySummaryDto(DateOnly Day, int Trips, int InvalidTrips, int DepotRuns, int Boardings, int Alightings, DateTime FirstStart, DateTime LastEnd);

/// <param name="Kind">UnexpectedRestart, PowerCutUntilEnd, PowerCutStatusFalse or InvalidPassengerCount (ADA's four).</param>
/// <param name="Source">LegacyAda or Detector.</param>
public sealed record VehicleFaultDto(long Id, int? DeviceNumber, long? TripId, DateTime From, DateTime? To, string Kind, string Source, string? Details);

public sealed record VehicleDetailDto(
    FleetVehicleDto Vehicle, IReadOnlyList<VehicleDeviceDto> Devices, IReadOnlyList<VehicleDaySummaryDto> TripDays,
    IReadOnlyList<VehicleFaultDto> Faults, IReadOnlyList<DateOnly> Days);
