using System.Globalization;
using System.Text.RegularExpressions;
using AdaPlatform.Domain.Network;
using AdaPlatform.Domain.Operations;
using AdaPlatform.Domain.Raw;
using AdaPlatform.Infrastructure.Import.Ucp;
using AdaPlatform.Infrastructure.Reporting;

namespace AdaPlatform.Infrastructure.Reconstruction;

/// <summary>Settings a deployment can override in the "Reconstruction" section.</summary>
public sealed record ReconstructionOptions
{
    public const string SectionName = "Reconstruction";

    /// <summary>
    /// A trip whose planned first or last stop name starts with this is a depot run.
    /// DPMB names its depots "Garaz ED Medlanky", "Garaz ED Pisarky", …
    /// </summary>
    public string DepotStopNamePrefix { get; init; } = "Garaz";
}

/// <summary>
/// Rebuilds trips, stop visits and door counts from one vehicle-day of UCP events (one
/// source file, in file order). Pure: it reads nothing and writes nothing, so it can be
/// re-run whenever the rules change and tested on real log excerpts.
///
/// How a UCP log describes a trip, and the rules that follow from it:
/// <list type="bullet">
/// <item><b>Trip:</b> a trip start (7) announces it: pattern, position in the block and the
/// planned first and last stop. Every counter then restarts (12) as the trip begins
/// (phase "zahajena jizda"); a restart after the trip has left its first stop is a
/// mid-trip restart. A trip start repeating the running trip's pattern and index
/// re-announces it and does not open a new one.</item>
/// <item><b>Late terminus:</b> the previous trip's terminus is often logged after the next
/// trip is announced, at a quick turnaround even after it has begun. Every line carries the
/// trip destination it was written under (log column 4), so events still stamped with the
/// previous trip's destination go to that trip until the new one departs.</item>
/// <item><b>Stop visit:</b> arrival (8), counting start (10), departure (9), the vehicle's
/// stop summary (15), or a pass (120) when the doors don't open.</item>
/// <item><b>Door count:</b> the stop reading (11) minus the start reading (10), see
/// <see cref="DoorStopPairing"/>. It belongs to the visit where the door <i>started</i>
/// counting: at a terminus the stop readings already carry the next trip's first stop.
/// A second stop reading right after the first adds the passengers counted late, if any.</item>
/// <item><b>Totals:</b> a visit's boardings and alightings are the sum over its doors; the
/// vehicle's summary is used only when no door count exists. Occupancy is the vehicle's
/// own on-board figure, kept negative if it drifted. A summary with negative counts (logged
/// across a counter restart) is not used.</item>
/// <item><b>Validity:</b> a trip is invalid when a device was flagged at one of its stops
/// (<c>chyba</c>), reported itself not alive during it, or started counting at a stop
/// without a usable stop reading.</item>
/// </list>
/// </summary>
public sealed partial class UcpTripReconstructor(ReconstructionOptions options)
{
    /// <param name="events">One source file's events, ordered by line number.</param>
    /// <param name="countingDeviceId">Database id of the vehicle's counting device with this number.</param>
    public ReconstructedDay Reconstruct(IEnumerable<DeviceEvent> events, Func<int, long?> countingDeviceId)
    {
        var run = new Run(options, countingDeviceId);
        foreach (var e in events)
        {
            run.Feed(e);
        }
        return run.Finish();
    }

    // "943002 (04:40) Garaz ED Medlanky" — the planned stop in a trip start message.
    [GeneratedRegex(@"^(?<code>\d+)\s*\(\d{1,2}:\d{2}\)\s*(?<name>.*)$")]
    private static partial Regex PlannedStopPattern();

    private sealed class Run(ReconstructionOptions options, Func<int, long?> countingDeviceId)
    {
        private readonly ReconstructedDay _day = new();
        private readonly List<OpenTrip> _trips = [];
        private readonly Dictionary<int, DeviceState> _devices = [];

        // The trip being run, and the one before it while its terminus may still be logged.
        private OpenTrip? _current;
        private OpenTrip? _previous;

        private ReconstructionStats Stats => _day.Stats;

        public void Feed(DeviceEvent e)
        {
            switch (e.Type)
            {
                case DeviceEventType.Init:
                case DeviceEventType.SystemShutdown:
                    // The on-board computer starts over; whatever runs next announces itself.
                    _current = _previous = null;
                    break;
                case DeviceEventType.TripStart:
                    OnTripStart(e);
                    break;
                case DeviceEventType.TripPhase:
                    OnTripPhase(e);
                    break;
                case DeviceEventType.DeviceRestart:
                    OnRestart(e);
                    break;
                case DeviceEventType.Heartbeat:
                    if (e.Alive == false && _current is { Started: true } trip)
                    {
                        trip.NotAliveDevices.Add(e.DeviceNumber);
                    }
                    break;
                case DeviceEventType.CountingStopped:
                    OnCountingStopped(e);
                    break;
                case DeviceEventType.Arrival:
                case DeviceEventType.Departure:
                case DeviceEventType.CountingStarted:
                case DeviceEventType.StopSummary:
                case DeviceEventType.StopPassed:
                    OnStopEvent(e);
                    break;
            }
        }

        public ReconstructedDay Finish()
        {
            foreach (var trip in _trips)
            {
                Complete(trip);
            }
            return _day;
        }

        private void OnTripStart(DeviceEvent e)
        {
            var payload = UcpLogParser.ParsePayload(e.Payload);
            var index = int.TryParse(payload.GetValueOrDefault("index"), CultureInfo.InvariantCulture, out var i) ? i : (int?)null;

            if (_current is { } running && running.PatternCode == e.PatternCode && running.Index == index)
            {
                Stats.RepeatedTripStarts++;
                return;
            }

            var first = ParsePlannedStop(payload.GetValueOrDefault("prvni_zast"));
            var last = ParsePlannedStop(payload.GetValueOrDefault("posl_zast"));
            var trip = new OpenTrip
            {
                VehicleId = e.VehicleId,
                AnnouncedAt = e.Time,
                PatternCode = e.PatternCode,
                Index = index,
                BlockCode = e.BlockCode,
                PlannedFirst = first,
                PlannedLast = last,
                Destination = e.LastStopCode ?? last?.Code,
            };
            _trips.Add(trip);
            _previous = _current;
            _current = trip;

            if (first is not null) SeeStop(first.Code, first.Name, null, null);
            if (last is not null) SeeStop(last.Code, last.Name, null, null);
            if (e.BlockCode is not null) _day.Blocks.Add(e.BlockCode);
            if (e.PatternCode is { } code && e.LineId is { } line && !_day.Patterns.ContainsKey(code))
            {
                _day.Patterns[code] = new PatternSighting(new Pattern
                {
                    Code = code,
                    LineId = line,
                    TargetCode = last?.Code ?? 0,
                    FirstStopName = first?.Name,
                    LastStopName = last?.Name,
                });
            }
        }

        private void OnTripPhase(DeviceEvent e)
        {
            switch (e.Payload?.Trim())
            {
                case "zahajena jizda" when _current is { } trip:
                    trip.Started = true;
                    break;
                case "prijezd na konecnou" when Route(e) is { } trip:
                    trip.ReachedTerminus = true;
                    break;
            }
        }

        private void OnRestart(DeviceEvent e)
        {
            var device = Device(e.DeviceNumber);
            device.Pairing.Feed(Reading(e));
            device.LastType = e.Type;
            device.StartVisit = null;

            if (_current is not { } trip)
            {
                return;
            }
            if (!trip.Departed)
            {
                // The counters reset as the trip begins: after the "zahajena jizda" phase, and at a
                // quick turnaround after the arrival at the first stop.
                trip.CountersReset = true;
                trip.Started = true;
            }
            else
            {
                Stats.MidTripRestarts++;
            }
        }

        private void OnCountingStopped(DeviceEvent e)
        {
            var device = Device(e.DeviceNumber);
            var repeated = device.LastType == DeviceEventType.CountingStopped;
            var visit = device.StartVisit;
            device.LastType = e.Type;

            if (repeated)
            {
                // A second stop reading, a few seconds after the first: passengers counted late.
                Stats.RepeatedStopReadings++;
                var boardings = (e.Boardings ?? 0) - device.LastStopBoardings;
                var alightings = (e.Alightings ?? 0) - device.LastStopAlightings;
                if (boardings >= 0 && alightings >= 0)
                {
                    device.LastStopBoardings += boardings;
                    device.LastStopAlightings += alightings;
                    if ((boardings > 0 || alightings > 0) && visit is not null)
                    {
                        visit.AddDoor(e.DeviceNumber, boardings, alightings);
                        Stats.LateCounts++;
                    }
                }
                return;
            }

            device.LastStopBoardings = e.Boardings ?? 0;
            device.LastStopAlightings = e.Alightings ?? 0;
            var count = device.Pairing.Feed(Reading(e));
            if (count is { } c && visit is not null)
            {
                visit.AddDoor(e.DeviceNumber, c.Boardings, c.Alightings);
            }
            else if (count is null)
            {
                Stats.UnusableStopReadings++;
            }
        }

        private void OnStopEvent(DeviceEvent e)
        {
            if (e.Type == DeviceEventType.CountingStarted)
            {
                var device = Device(e.DeviceNumber);
                device.Pairing.Feed(Reading(e));
                device.LastType = e.Type;
                device.StartVisit = null;
            }

            if (Route(e) is not { } trip)
            {
                Stats.EventsOutsideTrips++;
                return;
            }
            if (e.StopCode is not { } stopCode)
            {
                return;
            }

            var visit = trip.VisitAt(stopCode);
            var payload = e.Type is DeviceEventType.StopSummary or DeviceEventType.StopPassed
                ? UcpLogParser.ParsePayload(e.Payload)
                : null;
            SeeStop(stopCode, payload?.GetValueOrDefault(e.Type == DeviceEventType.StopSummary ? "zast_nazev" : "n"), e.Latitude, e.Longitude);

            switch (e.Type)
            {
                case DeviceEventType.Arrival:
                    visit.Arrival ??= e.Time;
                    visit.ArrivalDelay ??= e.DelaySeconds;
                    break;
                case DeviceEventType.Departure:
                    visit.Departure = e.Time;
                    visit.DepartureDelay = e.DelaySeconds;
                    if (trip == _current)
                    {
                        // Under way: nothing more of the previous trip can follow.
                        trip.Departed = true;
                        _previous = null;
                    }
                    break;
                case DeviceEventType.CountingStarted:
                    visit.StartedDevices.Add(e.DeviceNumber);
                    Device(e.DeviceNumber).StartVisit = visit;
                    break;
                case DeviceEventType.StopSummary:
                    visit.Summary = (e.Boardings ?? 0, e.Alightings ?? 0, e.OnBoard);
                    foreach (var flagged in DeviceNumbers(e.InvalidDevices))
                    {
                        visit.FlaggedDevices.Add(flagged);
                    }
                    break;
                case DeviceEventType.StopPassed:
                    visit.Arrival ??= e.Time;
                    visit.PassDelay = e.DelaySeconds;
                    break;
            }
        }

        /// <summary>The trip an event belongs to (see "Late terminus" above), or null before any trip start.</summary>
        private OpenTrip? Route(DeviceEvent e)
        {
            if (_previous is { } previous && _current is { } current
                && e.LastStopCode is { } destination && destination == previous.Destination && destination != current.Destination)
            {
                Stats.EventsForPreviousTrip++;
                return previous;
            }
            return _current;
        }

        private void Complete(OpenTrip open)
        {
            var visits = open.Visits;
            if (visits.Count == 0)
            {
                Stats.EmptyTrips++;
                return;
            }

            var trip = new Trip
            {
                VehicleId = open.VehicleId,
                PatternCode = open.PatternCode,
                BlockCode = open.BlockCode,
                Origin = DataOrigin.Measured,
                IsDepotRun = IsDepot(open.PlannedFirst) || IsDepot(open.PlannedLast),
            };

            var occupancy = 0;
            var flagged = false;
            var missing = false;
            foreach (var v in visits)
            {
                var visit = v.ToStopVisit(trip.StopVisits.Count + 1, ref occupancy, countingDeviceId, Stats);
                trip.StopVisits.Add(visit);
                flagged |= v.FlaggedDevices.Count > 0;
                missing |= v.StartedDevices.Any(d => !v.Doors.ContainsKey(d));
            }

            var firstVisit = trip.StopVisits[0];
            var lastVisit = trip.StopVisits[^1];
            trip.StartTime = firstVisit.DepartureTime ?? firstVisit.ArrivalTime ?? open.AnnouncedAt;
            trip.EndTime = lastVisit.ArrivalTime ?? lastVisit.DepartureTime ?? trip.StartTime;
            trip.InitialDelaySeconds = firstVisit.DelaySeconds;
            trip.Boardings = trip.StopVisits.Sum(s => s.Boardings);
            trip.Alightings = trip.StopVisits.Sum(s => s.Alightings);

            var notAlive = open.NotAliveDevices.Count > 0;
            trip.IsValid = !flagged && !notAlive && !missing;

            Stats.Trips++;
            if (trip.IsDepotRun) Stats.DepotRuns++;
            if (!trip.IsValid) Stats.InvalidTrips++;
            if (flagged) Stats.TripsWithFlaggedDevice++;
            if (notAlive) Stats.TripsWithDeviceNotAlive++;
            if (missing) Stats.TripsWithMissingDoorCount++;
            if (!open.ReachedTerminus) Stats.TripsWithoutTerminus++;
            if (!open.CountersReset) Stats.TripsWithoutCounterReset++;
            _day.Trips.Add(trip);

            // A pattern the timetable doesn't know gets its stops from a trip that ran all of it.
            if (open.PatternCode is { } code && _day.Patterns.TryGetValue(code, out var sighting) && sighting.StopCodes is null
                && open.ReachedTerminus && visits[0].StopCode == open.PlannedFirst?.Code && visits[^1].StopCode == open.Destination)
            {
                sighting.StopCodes = visits.Select(v => v.StopCode).ToList();
            }
        }

        private bool IsDepot(PlannedStop? stop) =>
            stop is not null && options.DepotStopNamePrefix.Length > 0
            && stop.Name.StartsWith(options.DepotStopNamePrefix, StringComparison.OrdinalIgnoreCase);

        private void SeeStop(int code, string? name, double? latitude, double? longitude)
        {
            if (!_day.Stops.TryGetValue(code, out var stop))
            {
                _day.Stops[code] = stop = new Stop { Code = code, Name = "" };
            }
            if (stop.Name.Length == 0 && !string.IsNullOrWhiteSpace(name)) stop.Name = name.Trim();
            if (stop.Latitude is null && latitude is not null)
            {
                stop.Latitude = latitude;
                stop.Longitude = longitude;
            }
        }

        private DeviceState Device(int number)
        {
            if (!_devices.TryGetValue(number, out var device))
            {
                _devices[number] = device = new DeviceState();
            }
            return device;
        }

        private static CounterReading Reading(DeviceEvent e) =>
            new(e.VehicleId, e.DeviceNumber, e.Time, e.Type, e.StopCode, e.Boardings ?? 0, e.Alightings ?? 0);

        private static PlannedStop? ParsePlannedStop(string? value)
        {
            var match = value is null ? null : PlannedStopPattern().Match(value);
            return match is { Success: true } && int.TryParse(match.Groups["code"].Value, CultureInfo.InvariantCulture, out var code) && code > 0
                ? new PlannedStop(code, match.Groups["name"].Value.Trim())
                : null;
        }

        // "42" or "41, 42" (brackets already stripped by the parser).
        private static IEnumerable<int> DeviceNumbers(string? value) =>
            (value ?? "").Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, CultureInfo.InvariantCulture, out var n) ? n : 0)
                .Where(n => n > 0);
    }

    private sealed record PlannedStop(int Code, string Name);

    private sealed class DeviceState
    {
        public DoorStopPairing Pairing { get; } = new();
        public DeviceEventType LastType { get; set; }
        public int LastStopBoardings { get; set; }
        public int LastStopAlightings { get; set; }

        /// <summary>Where the device last started counting; its next stop reading belongs there.</summary>
        public OpenVisit? StartVisit { get; set; }
    }

    private sealed class OpenTrip
    {
        public int VehicleId { get; init; }
        public DateTime AnnouncedAt { get; init; }
        public int? PatternCode { get; init; }
        public int? Index { get; init; }
        public string? BlockCode { get; init; }
        public PlannedStop? PlannedFirst { get; init; }
        public PlannedStop? PlannedLast { get; init; }
        public int? Destination { get; init; }

        public bool Started { get; set; }
        public bool Departed { get; set; }
        public bool CountersReset { get; set; }
        public bool ReachedTerminus { get; set; }
        public HashSet<int> NotAliveDevices { get; } = [];
        public List<OpenVisit> Visits { get; } = [];

        /// <summary>The visit at this stop: the current one if the vehicle is still there, else a new one.</summary>
        public OpenVisit VisitAt(int stopCode)
        {
            if (Visits.Count > 0 && Visits[^1].StopCode == stopCode)
            {
                return Visits[^1];
            }
            var visit = new OpenVisit(stopCode);
            Visits.Add(visit);
            return visit;
        }
    }

    private sealed class OpenVisit(int stopCode)
    {
        public int StopCode { get; } = stopCode;
        public DateTime? Arrival { get; set; }
        public DateTime? Departure { get; set; }
        public int? ArrivalDelay { get; set; }
        public int? DepartureDelay { get; set; }
        public int? PassDelay { get; set; }
        public (int Boardings, int Alightings, int? OnBoard)? Summary { get; set; }
        public HashSet<int> StartedDevices { get; } = [];
        public HashSet<int> FlaggedDevices { get; } = [];
        public Dictionary<int, (int Boardings, int Alightings)> Doors { get; } = [];

        public void AddDoor(int device, int boardings, int alightings)
        {
            var (b, a) = Doors.GetValueOrDefault(device);
            Doors[device] = (b + boardings, a + alightings);
        }

        public StopVisit ToStopVisit(int sequence, ref int occupancy, Func<int, long?> countingDeviceId, ReconstructionStats stats)
        {
            var visit = new StopVisit
            {
                Sequence = sequence,
                StopCode = StopCode,
                ArrivalTime = Arrival,
                DepartureTime = Departure,
                DelaySeconds = DepartureDelay ?? ArrivalDelay ?? PassDelay ?? 0,
                IsPassThrough = StartedDevices.Count == 0 && Doors.Count == 0 && Summary is null,
                Origin = DataOrigin.Measured,
            };

            foreach (var (device, (boardings, alightings)) in Doors.OrderBy(d => d.Key))
            {
                if (countingDeviceId(device) is not { } id)
                {
                    stats.UnknownDevices++;
                    continue;
                }
                visit.DoorCounts.Add(new DoorCount
                {
                    CountingDeviceId = id,
                    Boardings = boardings,
                    Alightings = alightings,
                    IsFlaggedInvalid = FlaggedDevices.Contains(device),
                    Origin = DataOrigin.Measured,
                });
            }

            if (Doors.Count > 0)
            {
                visit.Boardings = Doors.Values.Sum(d => d.Boardings);
                visit.Alightings = Doors.Values.Sum(d => d.Alightings);
                if (Summary is { } s && (s.Boardings, s.Alightings) != (visit.Boardings, visit.Alightings))
                {
                    stats.SummaryMismatches++;
                }
            }
            else if (Summary is { Boardings: >= 0, Alightings: >= 0 } s)
            {
                (visit.Boardings, visit.Alightings) = (s.Boardings, s.Alightings);
                stats.SummaryOnlyVisits++;
            }
            else if (Summary is not null)
            {
                stats.RejectedSummaries++;
            }

            occupancy = Summary?.OnBoard ?? occupancy + visit.Boardings - visit.Alightings;
            visit.Occupancy = occupancy;

            stats.StopVisits++;
            if (visit.IsPassThrough) stats.PassThroughs++;
            stats.DoorCounts += visit.DoorCounts.Count;
            stats.MissingDoorCounts += StartedDevices.Count(d => !Doors.ContainsKey(d));
            return visit;
        }
    }
}

/// <summary>What one vehicle-day's log yields: trips, and the reference data they refer to.</summary>
public sealed class ReconstructedDay
{
    /// <summary>Trips with their stop visits and door counts; <c>SourceFileId</c> is set by the caller.</summary>
    public List<Trip> Trips { get; } = [];

    /// <summary>Every stop the trips visit, with the name and position the log gives (name may be empty).</summary>
    public Dictionary<int, Stop> Stops { get; } = [];

    /// <summary>Patterns announced by trip starts, keyed by code.</summary>
    public Dictionary<int, PatternSighting> Patterns { get; } = [];

    public HashSet<string> Blocks { get; } = [];

    public ReconstructionStats Stats { get; } = new();
}

/// <summary>A pattern as a trip start describes it, and its stops if a trip ran the whole of it.</summary>
public sealed class PatternSighting(Pattern pattern)
{
    public Pattern Pattern { get; } = pattern;
    public List<int>? StopCodes { get; set; }
}

/// <summary>What reconstruction did and what it couldn't do — the evidence for the data report.</summary>
public sealed class ReconstructionStats
{
    public int Trips { get; set; }
    public int DepotRuns { get; set; }
    public int InvalidTrips { get; set; }
    public int TripsWithFlaggedDevice { get; set; }
    public int TripsWithDeviceNotAlive { get; set; }
    public int TripsWithMissingDoorCount { get; set; }
    public int TripsWithoutTerminus { get; set; }
    public int TripsWithoutCounterReset { get; set; }

    /// <summary>Announced trips that never reached a stop (e.g. announced, then switched off).</summary>
    public int EmptyTrips { get; set; }
    public int RepeatedTripStarts { get; set; }
    public int MidTripRestarts { get; set; }

    public int StopVisits { get; set; }
    public int PassThroughs { get; set; }

    /// <summary>Visits where the door counts don't add up to the vehicle's own summary.</summary>
    public int SummaryMismatches { get; set; }

    /// <summary>Visits with a vehicle summary but no door count; the summary is used.</summary>
    public int SummaryOnlyVisits { get; set; }

    /// <summary>Visits whose only figure is a summary with negative counts; left at zero.</summary>
    public int RejectedSummaries { get; set; }

    public int DoorCounts { get; set; }

    /// <summary>Doors that started counting at a visit but have no usable stop reading.</summary>
    public int MissingDoorCounts { get; set; }

    public int RepeatedStopReadings { get; set; }

    /// <summary>Repeated stop readings that added passengers.</summary>
    public int LateCounts { get; set; }

    /// <summary>Stop readings with no start before them, or lower than their start.</summary>
    public int UnusableStopReadings { get; set; }

    public int EventsForPreviousTrip { get; set; }
    public int EventsOutsideTrips { get; set; }
    public int UnknownDevices { get; set; }

    public void Add(ReconstructionStats o)
    {
        Trips += o.Trips;
        DepotRuns += o.DepotRuns;
        InvalidTrips += o.InvalidTrips;
        TripsWithFlaggedDevice += o.TripsWithFlaggedDevice;
        TripsWithDeviceNotAlive += o.TripsWithDeviceNotAlive;
        TripsWithMissingDoorCount += o.TripsWithMissingDoorCount;
        TripsWithoutTerminus += o.TripsWithoutTerminus;
        TripsWithoutCounterReset += o.TripsWithoutCounterReset;
        EmptyTrips += o.EmptyTrips;
        RepeatedTripStarts += o.RepeatedTripStarts;
        MidTripRestarts += o.MidTripRestarts;
        StopVisits += o.StopVisits;
        PassThroughs += o.PassThroughs;
        SummaryMismatches += o.SummaryMismatches;
        SummaryOnlyVisits += o.SummaryOnlyVisits;
        RejectedSummaries += o.RejectedSummaries;
        DoorCounts += o.DoorCounts;
        MissingDoorCounts += o.MissingDoorCounts;
        RepeatedStopReadings += o.RepeatedStopReadings;
        LateCounts += o.LateCounts;
        UnusableStopReadings += o.UnusableStopReadings;
        EventsForPreviousTrip += o.EventsForPreviousTrip;
        EventsOutsideTrips += o.EventsOutsideTrips;
        UnknownDevices += o.UnknownDevices;
    }

    public override string ToString() => $"""
          trips {Trips}: invalid {InvalidTrips} (flagged device {TripsWithFlaggedDevice}, device not alive {TripsWithDeviceNotAlive}, missing door count {TripsWithMissingDoorCount}), depot runs {DepotRuns}
            without terminus {TripsWithoutTerminus}, without counter reset {TripsWithoutCounterReset}, empty (dropped) {EmptyTrips}, repeated trip starts {RepeatedTripStarts}, mid-trip restarts {MidTripRestarts}
          stop visits {StopVisits}: pass-throughs {PassThroughs}, summary only {SummaryOnlyVisits} (negative summary rejected {RejectedSummaries}), door sum ≠ summary {SummaryMismatches}
          door counts {DoorCounts}: missing {MissingDoorCounts}, late counts {LateCounts} (of {RepeatedStopReadings} repeated readings), unusable stop readings {UnusableStopReadings}
          events for the previous trip {EventsForPreviousTrip}, outside any trip {EventsOutsideTrips}, unknown devices {UnknownDevices}
        """;
}
