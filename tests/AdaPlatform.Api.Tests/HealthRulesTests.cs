using AdaPlatform.Infrastructure.Reporting;

namespace AdaPlatform.Api.Tests;

/// <summary>
/// The health rules on plain numbers — every rule and threshold edge, without a database.
/// Thresholds are the defaults: imbalance 20 % / 35 %, negative occupancy 10 % / 25 %,
/// flagged stops 10 %, not-alive heartbeats 50 %, balance judged from 100 passengers.
/// </summary>
public sealed class HealthRulesTests
{
    private static readonly HealthThresholds T = new();

    private static DeviceStats Device(int stops = 100, int boardings = 300, int alightings = 290, int flagged = 0, int heartbeats = 50, int notAlive = 0) =>
        new(stops, boardings, alightings, flagged, heartbeats, notAlive);

    private static VehicleStats Vehicle(int devices = 2, int silent = 0, double? imbalance = 0.05, double? negative = 0.02, double? flagged = 0.01, bool deviceWarning = false) =>
        new(devices, silent, imbalance, negative, flagged, deviceWarning);

    private static HealthReason[] Codes(Assessment a) => [.. a.Reasons.Select(r => r.Code)];

    [Fact]
    public void A_device_that_counts_normally_is_ok()
    {
        var a = HealthRules.Device(Device(), T);
        Assert.Equal(HealthStatus.Ok, a.Status);
        Assert.Empty(a.Reasons);
    }

    [Fact]
    public void A_device_that_never_finished_counting_is_unknown()
    {
        Assert.Equal(HealthStatus.Unknown, HealthRules.Device(Device(stops: 0, boardings: 0, alightings: 0), T).Status);
    }

    [Fact]
    public void A_device_that_counted_nobody_at_its_stops_is_a_fault()
    {
        var a = HealthRules.Device(Device(stops: 40, boardings: 0, alightings: 0), T);
        Assert.Equal(HealthStatus.Fault, a.Status);
        Assert.Equal(new HealthReasonDto(HealthReason.DeviceSilent, 40), Assert.Single(a.Reasons));
    }

    [Theory]
    [InlineData(9, HealthStatus.Ok)]       // 9 % of stops flagged: below the 10 % warning
    [InlineData(10, HealthStatus.Warning)] // exactly at the threshold counts as reaching it
    public void Flagged_stops_warn_from_the_threshold(int flagged, HealthStatus expected)
    {
        Assert.Equal(expected, HealthRules.Device(Device(flagged: flagged), T).Status);
    }

    [Theory]
    [InlineData(24, HealthStatus.Ok)]
    [InlineData(25, HealthStatus.Warning)] // 25 of 50 heartbeats not alive = 50 %
    public void Not_alive_heartbeats_warn_from_the_threshold(int notAlive, HealthStatus expected)
    {
        Assert.Equal(expected, HealthRules.Device(Device(notAlive: notAlive), T).Status);
    }

    [Fact]
    public void A_silent_device_stays_a_fault_when_it_also_has_warnings()
    {
        var a = HealthRules.Device(Device(stops: 40, boardings: 0, alightings: 0, flagged: 20), T);
        Assert.Equal(HealthStatus.Fault, a.Status);
        Assert.Equal([HealthReason.DeviceSilent, HealthReason.DeviceFlagged], Codes(a));
    }

    [Fact]
    public void A_vehicle_without_counting_devices_is_unknown()
    {
        Assert.Equal(HealthStatus.Unknown, HealthRules.Vehicle(Vehicle(devices: 0, imbalance: null, negative: null, flagged: null), T).Status);
    }

    [Fact]
    public void All_devices_silent_is_a_fault_some_is_a_warning()
    {
        var all = HealthRules.Vehicle(Vehicle(devices: 2, silent: 2), T);
        var some = HealthRules.Vehicle(Vehicle(devices: 2, silent: 1), T);

        Assert.Equal((HealthStatus.Fault, HealthReason.AllDevicesSilent), (all.Status, all.Reasons.Single().Code));
        Assert.Equal((HealthStatus.Warning, new HealthReasonDto(HealthReason.SomeDevicesSilent, 1)), (some.Status, some.Reasons.Single()));
    }

    [Theory]
    [InlineData(0.19, HealthStatus.Ok)]
    [InlineData(0.20, HealthStatus.Warning)]
    [InlineData(0.34, HealthStatus.Warning)]
    [InlineData(0.35, HealthStatus.Fault)]
    public void Imbalance_warns_then_faults(double imbalance, HealthStatus expected)
    {
        Assert.Equal(expected, HealthRules.Vehicle(Vehicle(imbalance: imbalance), T).Status);
    }

    [Theory]
    [InlineData(0.09, HealthStatus.Ok)]
    [InlineData(0.10, HealthStatus.Warning)]
    [InlineData(0.25, HealthStatus.Fault)]
    public void Negative_occupancy_warns_then_faults(double share, HealthStatus expected)
    {
        Assert.Equal(expected, HealthRules.Vehicle(Vehicle(negative: share), T).Status);
    }

    [Fact]
    public void Flagged_stops_on_a_vehicle_only_ever_warn()
    {
        Assert.Equal(HealthStatus.Warning, HealthRules.Vehicle(Vehicle(flagged: 0.9), T).Status);
    }

    [Fact]
    public void A_device_warning_shows_only_on_an_otherwise_healthy_vehicle()
    {
        var healthy = HealthRules.Vehicle(Vehicle(deviceWarning: true), T);
        var faulty = HealthRules.Vehicle(Vehicle(imbalance: 0.5, deviceWarning: true), T);

        Assert.Equal((HealthStatus.Warning, HealthReason.DeviceWarning), (healthy.Status, healthy.Reasons.Single().Code));
        Assert.Equal([HealthReason.Imbalance], Codes(faulty));
    }

    [Theory]
    [InlineData(60, 39, null)]   // 99 passengers: too few to judge balance
    [InlineData(60, 40, 0.2)]    // 100: judged, |60 − 40| / 100
    [InlineData(0, 0, null)]     // nothing counted
    public void Imbalance_needs_enough_passengers(int boardings, int alightings, double? expected)
    {
        Assert.Equal(expected, QualityMetrics.Imbalance(boardings, alightings, T.MinPassengersForBalance));
    }

    [Fact]
    public void A_share_of_nothing_is_null_not_zero()
    {
        Assert.Null(QualityMetrics.Share(0, 0));
        Assert.Equal(0.25, QualityMetrics.Share(1, 4));
    }
}
