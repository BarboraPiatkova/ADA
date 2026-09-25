using AdaPlatform.Domain.Raw;
using AdaPlatform.Infrastructure.Reporting;

namespace AdaPlatform.Api.Tests;

/// <summary>
/// UCP in/out values are running counter readings; per-stop counts are stop − start.
/// Sequences modelled on the DPMB logs (e.g. device 45: start 0/0 → stop 547/616 after a restart).
/// </summary>
public class DoorStopPairingTests
{
    private static readonly DateTime T0 = new(2022, 8, 4, 7, 0, 0);

    private static CounterReading R(DeviceEventType type, int minute, int boardings, int alightings, int device = 41, int vehicle = 1711) =>
        new(vehicle, device, T0.AddMinutes(minute), type, 1000 + minute, boardings, alightings);

    [Fact]
    public void Count_at_a_stop_is_stop_reading_minus_start_reading()
    {
        var pairing = new DoorStopPairing();
        Assert.Null(pairing.Feed(R(DeviceEventType.CountingStarted, 0, 120, 100)));
        var count = pairing.Feed(R(DeviceEventType.CountingStopped, 1, 123, 104));

        Assert.NotNull(count);
        Assert.Equal((3, 4), (count.Value.Boardings, count.Value.Alightings));
        Assert.Equal((123, 104), (count.Value.ReadingBoardings, count.Value.ReadingAlightings));
    }

    [Fact]
    public void A_start_repeating_the_previous_stop_is_recognised()
    {
        var pairing = new DoorStopPairing();
        pairing.Feed(R(DeviceEventType.CountingStarted, 0, 0, 0));
        pairing.Feed(R(DeviceEventType.CountingStopped, 1, 547, 616));
        pairing.Feed(R(DeviceEventType.CountingStarted, 2, 547, 616));   // carries the reading forward
        var next = pairing.Feed(R(DeviceEventType.CountingStopped, 3, 550, 616));

        Assert.Equal(2, pairing.Starts);
        Assert.Equal(1, pairing.StartsRepeatingPreviousStop);
        Assert.Equal((3, 0), (next!.Value.Boardings, next.Value.Alightings));
    }

    [Fact]
    public void A_stop_right_after_a_restart_cannot_be_counted()
    {
        var pairing = new DoorStopPairing();
        pairing.Feed(R(DeviceEventType.CountingStarted, 0, 10, 10));
        pairing.Feed(R(DeviceEventType.DeviceRestart, 1, 0, 0));
        Assert.Null(pairing.Feed(R(DeviceEventType.CountingStopped, 2, 5, 5)));

        Assert.Equal(1, pairing.UnpairedStops);
        Assert.Equal(0, pairing.PairedStops);
    }

    [Fact]
    public void A_decreasing_reading_is_rejected_not_counted_negative()
    {
        var pairing = new DoorStopPairing();
        pairing.Feed(R(DeviceEventType.CountingStarted, 0, 50, 50));
        Assert.Null(pairing.Feed(R(DeviceEventType.CountingStopped, 1, 40, 50)));
        Assert.Equal(1, pairing.DecreasingStops);
    }

    [Fact]
    public void Readings_of_different_devices_never_pair()
    {
        var pairing = new DoorStopPairing();
        pairing.Feed(R(DeviceEventType.CountingStarted, 0, 0, 0, device: 41));
        Assert.Null(pairing.Feed(R(DeviceEventType.CountingStopped, 1, 5, 5, device: 42)));
        Assert.Equal(1, pairing.UnpairedStops);
    }
}
