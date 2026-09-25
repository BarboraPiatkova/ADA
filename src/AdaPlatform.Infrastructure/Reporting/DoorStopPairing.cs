using AdaPlatform.Domain.Raw;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>A counter reading from a UCP counting-start (10), counting-stop (11) or restart (12) message.</summary>
public readonly record struct CounterReading(
    int VehicleId, int DeviceNumber, DateTime Time, DeviceEventType Type, int? StopCode, int Boardings, int Alightings);

/// <summary>
/// Passengers one door counted at one stop: the stop reading minus the start reading.
/// The raw stop reading is kept too, for verifying the counter semantics (report F11).
/// </summary>
public readonly record struct DoorStopCount(
    int VehicleId, int DeviceNumber, DateTime Time, int? StopCode, int Boardings, int Alightings,
    int ReadingBoardings, int ReadingAlightings);

/// <summary>
/// Turns UCP counter readings into per-stop door counts.
///
/// The <c>in</c>/<c>out</c> values in messages 10, 11 and 12 are RUNNING COUNTER READINGS,
/// not per-stop counts: a counting-start (10) usually repeats the previous stop's reading,
/// and a counting-stop (11) is never lower than its start. The passengers counted at a stop
/// are therefore <c>stop − start</c>. Verified on the DPMB week — the vehicle's own stop
/// summary (15) equals this difference far more often than the raw stop reading (see the
/// data report, F11).
///
/// Feed readings ordered by (vehicle, device, time, line number).
/// </summary>
public sealed class DoorStopPairing
{
    private (int Vehicle, int Device)? _current;
    private CounterReading? _previous;

    public long Starts { get; private set; }
    public long StartsRepeatingPreviousStop { get; private set; }
    public long Stops { get; private set; }
    public long PairedStops { get; private set; }

    /// <summary>Stops not directly preceded by a start (e.g. after a restart) — no count can be derived.</summary>
    public long UnpairedStops { get; private set; }

    /// <summary>Stops whose reading is below their start — would mean the counter isn't monotonic.</summary>
    public long DecreasingStops { get; private set; }

    /// <summary>Returns the door count when <paramref name="reading"/> completes a start → stop pair.</summary>
    public DoorStopCount? Feed(CounterReading reading)
    {
        var key = (reading.VehicleId, reading.DeviceNumber);
        if (_current != key)
        {
            _current = key;
            _previous = null;
        }

        var previous = _previous;
        _previous = reading;

        switch (reading.Type)
        {
            case DeviceEventType.CountingStarted:
                Starts++;
                if (previous is { Type: DeviceEventType.CountingStopped } p
                    && p.Boardings == reading.Boardings && p.Alightings == reading.Alightings)
                {
                    StartsRepeatingPreviousStop++;
                }
                return null;

            case DeviceEventType.CountingStopped:
                Stops++;
                if (previous is not { Type: DeviceEventType.CountingStarted } start)
                {
                    UnpairedStops++;
                    return null;
                }
                if (reading.Boardings < start.Boardings || reading.Alightings < start.Alightings)
                {
                    DecreasingStops++;
                    return null;
                }
                PairedStops++;
                return new DoorStopCount(reading.VehicleId, reading.DeviceNumber, reading.Time, reading.StopCode,
                    reading.Boardings - start.Boardings, reading.Alightings - start.Alightings,
                    reading.Boardings, reading.Alightings);

            default:
                // A restart resets the counter; the next start carries the new baseline.
                return null;
        }
    }

    /// <summary>
    /// Streams all counter readings from the database through a pairing: a new one unless
    /// the caller wants its statistics afterwards (report F11).
    /// </summary>
    public static async IAsyncEnumerable<DoorStopCount> StreamAsync(
        AppDbContext db, DoorStopPairing? pairing = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        pairing ??= new DoorStopPairing();
        var readings = db.DeviceEvents.AsNoTracking()
            .Where(e => e.DeviceNumber > 0 && (e.Type == DeviceEventType.CountingStarted
                                               || e.Type == DeviceEventType.CountingStopped
                                               || e.Type == DeviceEventType.DeviceRestart))
            .OrderBy(e => e.VehicleId).ThenBy(e => e.DeviceNumber).ThenBy(e => e.Time).ThenBy(e => e.SourceFileId).ThenBy(e => e.LineNumber)
            .Select(e => new CounterReading(e.VehicleId, e.DeviceNumber, e.Time, e.Type, e.StopCode, e.Boardings ?? 0, e.Alightings ?? 0))
            .AsAsyncEnumerable();

        await foreach (var reading in readings.WithCancellation(ct))
        {
            if (pairing.Feed(reading) is { } count)
            {
                yield return count;
            }
        }
    }

    /// <summary>Door counts added up by <paramref name="keyOf"/>: stops, boardings and alightings per key.</summary>
    public static async Task<Dictionary<TKey, DoorTotals>> SumAsync<TKey>(
        AppDbContext db, Func<DoorStopCount, TKey> keyOf, CancellationToken ct = default)
        where TKey : notnull
    {
        var totals = new Dictionary<TKey, DoorTotals>();
        await foreach (var count in StreamAsync(db, ct: ct))
        {
            var key = keyOf(count);
            var t = totals.GetValueOrDefault(key);
            totals[key] = new DoorTotals(t.Stops + 1, t.Boardings + count.Boardings, t.Alightings + count.Alightings);
        }
        return totals;
    }
}

/// <summary>Stops counted and passengers counted, summed over some grouping.</summary>
public readonly record struct DoorTotals(int Stops, int Boardings, int Alightings);
