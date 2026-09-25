namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// The measures every quality report shares, defined once so the API, the heatmap and the
/// thesis data report (DatasetProfiler) can't drift apart.
/// </summary>
public static class QualityMetrics
{
    /// <summary>|in − out| / (in + out); null below the minimum passengers for a fair judgement.</summary>
    public static double? Imbalance(int boardings, int alightings, int minPassengers) =>
        boardings + alightings >= minPassengers && boardings + alightings > 0
            ? Math.Abs(boardings - alightings) / (double)(boardings + alightings)
            : null;

    /// <summary>part / total; null when there is nothing to share (so "no data" never reads as 0 %).</summary>
    public static double? Share(int part, int total) => total > 0 ? part / (double)total : null;

    /// <summary>Finished counting at stops but counted nobody: the clearest sign of a dead sensor.</summary>
    public static bool IsSilent(int stops, int boardings, int alightings) => stops > 0 && boardings + alightings == 0;
}

/// <summary>What one counting device measured over the period.</summary>
public sealed record DeviceStats(int Stops, int Boardings, int Alightings, int FlaggedStops, int Heartbeats, int NotAliveHeartbeats);

/// <summary>What one vehicle measured over the period, with its devices' verdicts.</summary>
public sealed record VehicleStats(
    int CountingDevices,
    int SilentDevices,
    double? Imbalance,
    double? NegativeOccupancyShare,
    double? FlaggedStopShare,
    bool AnyDeviceWarning);

public sealed record Assessment(HealthStatus Status, IReadOnlyList<HealthReasonDto> Reasons);

/// <summary>
/// The health rules themselves — pure functions of measured numbers and thresholds, with no
/// database, so each rule and its threshold edge can be unit-tested (HealthRulesTests).
/// A value at a threshold counts as reaching it (<c>&gt;=</c>), as the UI shows it.
/// </summary>
public static class HealthRules
{
    public static Assessment Device(DeviceStats d, HealthThresholds t)
    {
        var reasons = new List<HealthReasonDto>();
        var status = d.Stops == 0 ? HealthStatus.Unknown : HealthStatus.Ok;

        if (QualityMetrics.IsSilent(d.Stops, d.Boardings, d.Alightings))
        {
            reasons.Add(new(HealthReason.DeviceSilent, d.Stops));
            status = HealthStatus.Fault;
        }
        if (QualityMetrics.Share(d.FlaggedStops, d.Stops) is { } flagged && flagged >= t.FlaggedStopsWarning)
        {
            reasons.Add(new(HealthReason.DeviceFlagged, flagged));
            status = Worse(status, HealthStatus.Warning);
        }
        if (QualityMetrics.Share(d.NotAliveHeartbeats, d.Heartbeats) is { } notAlive && notAlive >= t.NotAliveWarning)
        {
            reasons.Add(new(HealthReason.DeviceNotAlive, notAlive));
            status = Worse(status, HealthStatus.Warning);
        }
        return new(status, reasons);
    }

    public static Assessment Vehicle(VehicleStats v, HealthThresholds t)
    {
        var reasons = new List<HealthReasonDto>();
        var status = v.CountingDevices > 0 ? HealthStatus.Ok : HealthStatus.Unknown;

        if (v.SilentDevices > 0)
        {
            var all = v.SilentDevices == v.CountingDevices;
            reasons.Add(all ? new(HealthReason.AllDevicesSilent) : new(HealthReason.SomeDevicesSilent, v.SilentDevices));
            status = Worse(status, all ? HealthStatus.Fault : HealthStatus.Warning);
        }
        if (v.Imbalance is { } imbalance && imbalance >= t.ImbalanceWarning)
        {
            reasons.Add(new(HealthReason.Imbalance, imbalance));
            status = Worse(status, imbalance >= t.ImbalanceFault ? HealthStatus.Fault : HealthStatus.Warning);
        }
        if (v.NegativeOccupancyShare is { } negative && negative >= t.NegativeOccupancyWarning)
        {
            reasons.Add(new(HealthReason.NegativeOccupancy, negative));
            status = Worse(status, negative >= t.NegativeOccupancyFault ? HealthStatus.Fault : HealthStatus.Warning);
        }
        if (v.FlaggedStopShare is { } flagged && flagged >= t.FlaggedStopsWarning)
        {
            reasons.Add(new(HealthReason.FlaggedStops, flagged));
            status = Worse(status, HealthStatus.Warning);
        }
        // A device's warning only shows on a vehicle that is otherwise fine.
        if (v.AnyDeviceWarning && status == HealthStatus.Ok)
        {
            reasons.Add(new(HealthReason.DeviceWarning));
            status = HealthStatus.Warning;
        }
        return new(status, reasons);
    }

    private static HealthStatus Worse(HealthStatus current, HealthStatus candidate) =>
        Rank(candidate) > Rank(current) ? candidate : current;

    private static int Rank(HealthStatus s) => s switch
    {
        HealthStatus.Ok => 1,
        HealthStatus.Warning => 2,
        HealthStatus.Fault => 3,
        _ => 0, // Unknown
    };
}
