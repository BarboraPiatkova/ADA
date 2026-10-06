using System.ComponentModel;
using AdaPlatform.Domain.Raw;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AdaPlatform.Infrastructure.Reporting;

public enum HealthStatus
{
    Ok,
    Warning,
    Fault,

    /// <summary>Too little data to judge.</summary>
    Unknown,
}

/// <summary>
/// Rule-based health of vehicles and counting devices, computed from raw events.
/// The thresholds are PROVISIONAL — a first cut from the DPMB week (see the data report,
/// F6–F8). Calibrating them is part of the thesis; they are returned with every report so
/// the UI shows which rule fired.
/// </summary>
public sealed record HealthThresholds
{
    /// <summary>Configuration section a deployment overrides them in.</summary>
    public const string SectionName = "Quality:Thresholds";

    /// <summary>Minimum counted passengers before in/out balance is judged.</summary>
    public int MinPassengersForBalance { get; init; } = 100;

    public double ImbalanceWarning { get; init; } = 0.20;
    public double ImbalanceFault { get; init; } = 0.35;

    public double NegativeOccupancyWarning { get; init; } = 0.10;
    public double NegativeOccupancyFault { get; init; } = 0.25;

    public double FlaggedStopsWarning { get; init; } = 0.10;
    public double NotAliveWarning { get; init; } = 0.50;
}

/// <summary>
/// Why a status was given — a code plus the measured value, so the UI can phrase it in any
/// language (the API returns no display text).
/// </summary>
public enum HealthReason
{
    /// <summary>Device: finished counting at stops but counted nobody. Value = stops.</summary>
    DeviceSilent,

    /// <summary>Device: flagged invalid (<c>chyba</c>) at this share of its stops.</summary>
    DeviceFlagged,

    /// <summary>Device: this share of its heartbeats report alive=false.</summary>
    DeviceNotAlive,

    /// <summary>Vehicle: every counting device is silent.</summary>
    AllDevicesSilent,

    /// <summary>Vehicle: some devices are silent. Value = how many.</summary>
    SomeDevicesSilent,

    /// <summary>Vehicle: |in − out| / (in + out) over the period.</summary>
    Imbalance,

    /// <summary>Vehicle: share of stops after which occupancy is negative.</summary>
    NegativeOccupancy,

    /// <summary>Vehicle: share of stops with a device flagged invalid.</summary>
    FlaggedStops,

    /// <summary>Vehicle: at least one of its devices has a warning.</summary>
    DeviceWarning,
}

public sealed record HealthReasonDto(HealthReason Code, double? Value = null);

public sealed record DeviceHealthDto(
    int DeviceNumber,
    string? FirmwareVersion,
    int StopsCounted,
    int Boardings,
    int Alightings,
    int Heartbeats,
    int NotAliveHeartbeats,
    int Restarts,
    int FlaggedStops,
    HealthStatus Status,
    IReadOnlyList<HealthReasonDto> Reasons);

public sealed record VehicleHealthDto(
    int VehicleId,
    string? Traction,
    string? Model,
    int Days,
    int Boardings,
    int Alightings,
    /// <summary>|in − out| / (in + out); null when too few passengers were counted.</summary>
    double? Imbalance,
    int StopSummaries,
    double? NegativeOccupancyShare,
    double? FlaggedStopShare,
    int SilentDevices,
    HealthStatus Status,
    IReadOnlyList<HealthReasonDto> Reasons,
    IReadOnlyList<DeviceHealthDto> Devices);

// Immutable: the report cache hands out the same instance instead of a copy per request.
[ImmutableObject(true)]
/// <param name="Days">Every service day with a vehicle log, whatever period the report covers: the period picker's days.</param>
public sealed record DeviceHealthReportDto(
    DateOnly? From,
    DateOnly? To,
    HealthThresholds Thresholds,
    IReadOnlyList<VehicleHealthDto> Vehicles,
    IReadOnlyList<DateOnly> Days);

public sealed class DeviceHealthReport(AppDbContext db, IOptions<HealthThresholds> options, HybridCache cache, DayCalendar calendar)
{
    /// <summary>The report for the current data and period, from the cache when it's still valid.</summary>
    public Task<DeviceHealthReportDto> GetAsync(ReportPeriod period = default, CancellationToken ct = default) =>
        ReportCache.GetAsync(db, cache, $"quality/devices/{period.Key}", token => BuildAsync(period, token), ct);

    public async Task<DeviceHealthReportDto> BuildAsync(ReportPeriod period = default, CancellationToken ct = default)
    {
        var thresholds = options.Value;
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        // The period picks log files (vehicle × service day); every count below comes from those alone.
        var files = await period.SourceFilesAsync(db, calendar, ct);
        var serviceDays = await ReportPeriod.ServiceDaysAsync(db, ct);
        var sourceFiles = db.SourceFiles.AsNoTracking();
        var deviceEvents = db.DeviceEvents.AsNoTracking();
        if (files is not null)
        {
            sourceFiles = sourceFiles.Where(f => files.Contains(f.Id));
            deviceEvents = deviceEvents.Where(e => files.Contains(e.SourceFileId));
        }

        var days = await sourceFiles
            .GroupBy(f => f.VehicleId)
            .Select(g => new { VehicleId = g.Key, Days = g.Select(f => f.ServiceDate).Distinct().Count(), From = g.Min(f => f.ServiceDate), To = g.Max(f => f.ServiceDate) })
            .ToListAsync(ct);
        if (days.Count == 0)
        {
            return new DeviceHealthReportDto(null, null, thresholds, [], serviceDays);
        }

        // Heartbeats and restarts per device, counted directly.
        var perDevice = await deviceEvents
            .Where(e => e.DeviceNumber > 0 && (e.Type == DeviceEventType.Heartbeat || e.Type == DeviceEventType.DeviceRestart))
            .GroupBy(e => new { e.VehicleId, e.DeviceNumber, e.Type })
            .Select(g => new { g.Key.VehicleId, g.Key.DeviceNumber, g.Key.Type, Count = g.Count(), NotAlive = g.Count(e => e.Alive == false) })
            .ToListAsync(ct);

        // Passengers per door: counter readings are running totals, so pair start → stop
        // and take the difference (see DoorStopPairing).
        var doors = await DoorStopPairing.SumAsync(db, c => (c.VehicleId, c.DeviceNumber), ct, files);

        var summaries = await deviceEvents
            .Where(e => e.Type == DeviceEventType.StopSummary)
            .GroupBy(e => e.VehicleId)
            .Select(g => new { VehicleId = g.Key, Total = g.Count(), Negative = g.Count(e => e.OnBoard < 0), Flagged = g.Count(e => e.InvalidDevices != null) })
            .ToDictionaryAsync(s => s.VehicleId, ct);

        // "chyba" lists device numbers ("41 42"); count per device in memory (≈11k rows).
        var flaggedPerDevice = (await deviceEvents
                .Where(e => e.Type == DeviceEventType.StopSummary && e.InvalidDevices != null)
                .Select(e => new { e.VehicleId, e.InvalidDevices })
                .ToListAsync(ct))
            .SelectMany(e => e.InvalidDevices!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => int.TryParse(d, out var n) ? n : 0)
                .Where(n => n > 0)
                .Select(n => (e.VehicleId, Device: n)))
            .GroupBy(k => k)
            .ToDictionary(g => g.Key, g => g.Count());

        var firmware = await db.CountingDevices.AsNoTracking()
            .ToDictionaryAsync(d => (d.VehicleId, d.DeviceNumber), d => d.FirmwareVersion, ct);
        var vehicles = await db.Vehicles.AsNoTracking().ToDictionaryAsync(v => v.Id, ct);

        // Lookups instead of scanning every device's rows once per vehicle.
        var eventCounts = perDevice.ToDictionary(p => (p.VehicleId, p.DeviceNumber, p.Type));
        var devicesOf = perDevice.Select(p => (p.VehicleId, p.DeviceNumber))
            .Concat(doors.Keys)
            .Distinct()
            .ToLookup(k => k.VehicleId, k => k.DeviceNumber);

        var rows = new List<VehicleHealthDto>();
        foreach (var vehicle in days.OrderBy(d => d.VehicleId))
        {
            var devices = devicesOf[vehicle.VehicleId].Order().Select(number =>
            {
                var heartbeats = eventCounts.GetValueOrDefault((vehicle.VehicleId, number, DeviceEventType.Heartbeat));
                var restarts = eventCounts.GetValueOrDefault((vehicle.VehicleId, number, DeviceEventType.DeviceRestart));
                var door = doors.GetValueOrDefault((vehicle.VehicleId, number));
                var flagged = flaggedPerDevice.GetValueOrDefault((vehicle.VehicleId, number));
                var verdict = HealthRules.Device(
                    new DeviceStats(door.Stops, door.Boardings, door.Alightings, flagged, heartbeats?.Count ?? 0, heartbeats?.NotAlive ?? 0),
                    thresholds);

                return new DeviceHealthDto(number, firmware.GetValueOrDefault((vehicle.VehicleId, number)), door.Stops,
                    door.Boardings, door.Alightings, heartbeats?.Count ?? 0, heartbeats?.NotAlive ?? 0,
                    restarts?.Count ?? 0, flagged, verdict.Status, verdict.Reasons);
            }).ToList();

            var boardings = devices.Sum(d => d.Boardings);
            var alightings = devices.Sum(d => d.Alightings);
            var imbalance = QualityMetrics.Imbalance(boardings, alightings, thresholds.MinPassengersForBalance);
            var summary = summaries.GetValueOrDefault(vehicle.VehicleId);
            var negativeShare = QualityMetrics.Share(summary?.Negative ?? 0, summary?.Total ?? 0);
            var flaggedShare = QualityMetrics.Share(summary?.Flagged ?? 0, summary?.Total ?? 0);
            var silent = devices.Count(d => QualityMetrics.IsSilent(d.StopsCounted, d.Boardings, d.Alightings));

            var assessment = HealthRules.Vehicle(
                new VehicleStats(devices.Count(d => d.StopsCounted > 0), silent, imbalance, negativeShare, flaggedShare,
                    devices.Any(d => d.Status == HealthStatus.Warning)),
                thresholds);

            vehicles.TryGetValue(vehicle.VehicleId, out var info);
            rows.Add(new VehicleHealthDto(vehicle.VehicleId, info?.Traction, info?.Model, vehicle.Days,
                boardings, alightings, imbalance, summary?.Total ?? 0, negativeShare, flaggedShare, silent,
                assessment.Status, assessment.Reasons, devices));
        }

        return new DeviceHealthReportDto(days.Min(d => d.From), days.Max(d => d.To), thresholds, rows, serviceDays);
    }
}
