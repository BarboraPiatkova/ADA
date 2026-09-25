using AdaPlatform.Domain.Raw;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// One vehicle on one operating day — the cell of the fleet × day heatmap. Shares are
/// null when the vehicle made no stops that day, so "no data" never reads as "0 %".
/// </summary>
public sealed record VehicleDayDto(
    int VehicleId,
    DateOnly Day,
    int Boardings,
    int Alightings,
    /// <summary>|in − out| / (in + out); null below the report's minimum passenger count.</summary>
    double? Imbalance,
    int StopSummaries,
    double? NegativeOccupancyShare,
    double? FlaggedStopShare);

/// <summary>
/// Per-vehicle, per-day health metrics from the raw layer: the same measures as
/// <see cref="DeviceHealthReport"/>, but by calendar day, so a reader can see when a
/// problem started and whether it is one vehicle or one day.
/// </summary>
public sealed class DailyQualityReport(AppDbContext db)
{
    public async Task<IReadOnlyList<VehicleDayDto>> BuildAsync(HealthThresholds thresholds, CancellationToken ct = default)
    {
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        var summaries = await db.DeviceEvents.AsNoTracking()
            .Where(e => e.Type == DeviceEventType.StopSummary)
            .GroupBy(e => new { e.VehicleId, Day = e.Time.Date })
            .Select(g => new
            {
                g.Key.VehicleId,
                g.Key.Day,
                Total = g.Count(),
                Negative = g.Count(e => e.OnBoard < 0),
                Flagged = g.Count(e => e.InvalidDevices != null),
            })
            .ToDictionaryAsync(s => (s.VehicleId, DateOnly.FromDateTime(s.Day)), ct);

        // Passengers per stop are counter differences (see DoorStopPairing).
        var passengers = await DoorStopPairing.SumAsync(db, c => (c.VehicleId, DateOnly.FromDateTime(c.Time)), ct);

        return summaries.Keys.Union(passengers.Keys)
            .Order()
            .Select(key =>
            {
                var door = passengers.GetValueOrDefault(key);
                var summary = summaries.GetValueOrDefault(key);
                var total = summary?.Total ?? 0;
                return new VehicleDayDto(
                    key.Item1,
                    key.Item2,
                    door.Boardings,
                    door.Alightings,
                    QualityMetrics.Imbalance(door.Boardings, door.Alightings, thresholds.MinPassengersForBalance),
                    total,
                    QualityMetrics.Share(summary?.Negative ?? 0, total),
                    QualityMetrics.Share(summary?.Flagged ?? 0, total));
            })
            .ToList();
    }
}
