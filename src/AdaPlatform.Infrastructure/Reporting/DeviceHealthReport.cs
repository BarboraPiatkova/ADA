using AdaPlatform.Domain.Raw;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
    /// <summary>Minimum counted passengers before in/out balance is judged.</summary>
    public int MinPassengersForBalance { get; init; } = 100;

    public double ImbalanceWarning { get; init; } = 0.20;
    public double ImbalanceFault { get; init; } = 0.35;

    public double NegativeOccupancyWarning { get; init; } = 0.10;
    public double NegativeOccupancyFault { get; init; } = 0.25;

    public double FlaggedStopsWarning { get; init; } = 0.10;
    public double NotAliveWarning { get; init; } = 0.50;
}

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
    IReadOnlyList<string> Reasons);

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
    IReadOnlyList<string> Reasons,
    IReadOnlyList<DeviceHealthDto> Devices);

public sealed record DeviceHealthReportDto(
    DateOnly? From,
    DateOnly? To,
    HealthThresholds Thresholds,
    IReadOnlyList<VehicleHealthDto> Vehicles);

public sealed class DeviceHealthReport(AppDbContext db)
{
    public async Task<DeviceHealthReportDto> BuildAsync(HealthThresholds thresholds, CancellationToken ct = default)
    {
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        var days = await db.SourceFiles.AsNoTracking()
            .GroupBy(f => f.VehicleId)
            .Select(g => new { VehicleId = g.Key, Days = g.Select(f => f.ServiceDate).Distinct().Count(), From = g.Min(f => f.ServiceDate), To = g.Max(f => f.ServiceDate) })
            .ToListAsync(ct);
        if (days.Count == 0)
        {
            return new DeviceHealthReportDto(null, null, thresholds, []);
        }

        // Heartbeats and restarts per device, counted directly.
        var perDevice = await db.DeviceEvents.AsNoTracking()
            .Where(e => e.DeviceNumber > 0 && (e.Type == DeviceEventType.Heartbeat || e.Type == DeviceEventType.DeviceRestart))
            .GroupBy(e => new { e.VehicleId, e.DeviceNumber, e.Type })
            .Select(g => new { g.Key.VehicleId, g.Key.DeviceNumber, g.Key.Type, Count = g.Count(), NotAlive = g.Count(e => e.Alive == false) })
            .ToListAsync(ct);

        // Passengers per door: counter readings are running totals, so pair start → stop
        // and take the difference (see DoorStopPairing).
        var doors = new Dictionary<(int Vehicle, int Device), (int Stops, int In, int Out)>();
        await foreach (var count in DoorStopPairing.StreamAsync(db, new DoorStopPairing(), ct))
        {
            var key = (count.VehicleId, count.DeviceNumber);
            var (stops, boardings, alightings) = doors.GetValueOrDefault(key);
            doors[key] = (stops + 1, boardings + count.Boardings, alightings + count.Alightings);
        }

        var summaries = await db.DeviceEvents.AsNoTracking()
            .Where(e => e.Type == DeviceEventType.StopSummary)
            .GroupBy(e => e.VehicleId)
            .Select(g => new { VehicleId = g.Key, Total = g.Count(), Negative = g.Count(e => e.OnBoard < 0), Flagged = g.Count(e => e.InvalidDevices != null) })
            .ToDictionaryAsync(s => s.VehicleId, ct);

        // "chyba" lists device numbers ("41 42"); count per device in memory (≈11k rows).
        var flaggedPerDevice = (await db.DeviceEvents.AsNoTracking()
                .Where(e => e.Type == DeviceEventType.StopSummary && e.InvalidDevices != null)
                .Select(e => new { e.VehicleId, e.InvalidDevices })
                .ToListAsync(ct))
            .SelectMany(e => e.InvalidDevices!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => int.TryParse(d, out var n) ? (e.VehicleId, Device: n) : (e.VehicleId, Device: -1)))
            .Where(k => k.Device > 0)
            .GroupBy(k => k)
            .ToDictionary(g => g.Key, g => g.Count());

        var firmware = await db.CountingDevices.AsNoTracking()
            .ToDictionaryAsync(d => (d.VehicleId, d.DeviceNumber), d => d.FirmwareVersion, ct);
        var vehicles = await db.Vehicles.AsNoTracking().ToDictionaryAsync(v => v.Id, ct);

        var rows = new List<VehicleHealthDto>();
        foreach (var vehicle in days.OrderBy(d => d.VehicleId))
        {
            var events = perDevice.Where(p => p.VehicleId == vehicle.VehicleId).ToList();
            var deviceNumbers = events.Select(p => p.DeviceNumber)
                .Concat(doors.Keys.Where(k => k.Vehicle == vehicle.VehicleId).Select(k => k.Device))
                .Distinct().Order();
            var devices = deviceNumbers.Select(number =>
            {
                var heartbeats = events.FirstOrDefault(p => p.DeviceNumber == number && p.Type == DeviceEventType.Heartbeat);
                var restarts = events.FirstOrDefault(p => p.DeviceNumber == number && p.Type == DeviceEventType.DeviceRestart);
                var (stops, doorIn, doorOut) = doors.GetValueOrDefault((vehicle.VehicleId, number));
                var flagged = flaggedPerDevice.GetValueOrDefault((vehicle.VehicleId, number));

                var reasons = new List<string>();
                var status = stops == 0 ? HealthStatus.Unknown : HealthStatus.Ok;
                if (stops > 0 && doorIn + doorOut == 0)
                {
                    reasons.Add($"za {stops} zastavení nenapočítala nikoho");
                    status = HealthStatus.Fault;
                }
                if (stops > 0 && flagged / (double)stops >= thresholds.FlaggedStopsWarning)
                {
                    reasons.Add($"označena jako chybná na {flagged / (double)stops:P0} zastavení");
                    status = Worse(status, HealthStatus.Warning);
                }
                if (heartbeats is { Count: > 0 } && heartbeats.NotAlive / (double)heartbeats.Count >= thresholds.NotAliveWarning)
                {
                    reasons.Add($"{heartbeats.NotAlive / (double)heartbeats.Count:P0} zpráv o stavu hlásí alive=false");
                    status = Worse(status, HealthStatus.Warning);
                }

                return new DeviceHealthDto(number, firmware.GetValueOrDefault((vehicle.VehicleId, number)), stops,
                    doorIn, doorOut, heartbeats?.Count ?? 0, heartbeats?.NotAlive ?? 0,
                    restarts?.Count ?? 0, flagged, status, reasons);
            }).ToList();

            var boardings = devices.Sum(d => d.Boardings);
            var alightings = devices.Sum(d => d.Alightings);
            double? imbalance = boardings + alightings >= thresholds.MinPassengersForBalance
                ? Math.Abs(boardings - alightings) / (double)(boardings + alightings)
                : null;
            var summary = summaries.GetValueOrDefault(vehicle.VehicleId);
            double? negativeShare = summary is { Total: > 0 } ? summary.Negative / (double)summary.Total : null;
            double? flaggedShare = summary is { Total: > 0 } ? summary.Flagged / (double)summary.Total : null;
            var silent = devices.Count(d => d.StopsCounted > 0 && d.Boardings + d.Alightings == 0);

            var vehicleReasons = new List<string>();
            var vehicleStatus = devices.Any(d => d.StopsCounted > 0) ? HealthStatus.Ok : HealthStatus.Unknown;
            if (silent > 0)
            {
                var all = silent == devices.Count(d => d.StopsCounted > 0);
                vehicleReasons.Add(all ? "žádná jednotka nenapočítala nikoho" : $"{silent} jednotek nenapočítalo nikoho");
                vehicleStatus = Worse(vehicleStatus, all ? HealthStatus.Fault : HealthStatus.Warning);
            }
            if (imbalance is { } i && i >= thresholds.ImbalanceWarning)
            {
                vehicleReasons.Add($"nesoulad nástupů a výstupů {i:P0}");
                vehicleStatus = Worse(vehicleStatus, i >= thresholds.ImbalanceFault ? HealthStatus.Fault : HealthStatus.Warning);
            }
            if (negativeShare is { } n && n >= thresholds.NegativeOccupancyWarning)
            {
                vehicleReasons.Add($"záporná obsazenost na {n:P0} zastavení");
                vehicleStatus = Worse(vehicleStatus, n >= thresholds.NegativeOccupancyFault ? HealthStatus.Fault : HealthStatus.Warning);
            }
            if (flaggedShare is { } f && f >= thresholds.FlaggedStopsWarning)
            {
                vehicleReasons.Add($"příznak chyby na {f:P0} zastavení");
                vehicleStatus = Worse(vehicleStatus, HealthStatus.Warning);
            }
            if (devices.Any(d => d.Status == HealthStatus.Warning) && vehicleStatus == HealthStatus.Ok)
            {
                vehicleReasons.Add("varování u některé jednotky");
                vehicleStatus = HealthStatus.Warning;
            }

            vehicles.TryGetValue(vehicle.VehicleId, out var info);
            rows.Add(new VehicleHealthDto(vehicle.VehicleId, info?.Traction, info?.Model, vehicle.Days,
                boardings, alightings, imbalance, summary?.Total ?? 0, negativeShare, flaggedShare, silent,
                vehicleStatus, vehicleReasons, devices));
        }

        return new DeviceHealthReportDto(days.Min(d => d.From), days.Max(d => d.To), thresholds, rows);
    }

    private static HealthStatus Worse(HealthStatus current, HealthStatus candidate) =>
        Rank(candidate) > Rank(current) ? candidate : current;

    private static int Rank(HealthStatus s) => s switch
    {
        HealthStatus.Unknown => 0,
        HealthStatus.Ok => 1,
        HealthStatus.Warning => 2,
        HealthStatus.Fault => 3,
        _ => 0,
    };
}
