namespace AdaPlatform.Domain.Raw;

/// <summary>
/// One line of a counting log, exactly as the vehicle reported it. This layer is never
/// modified after ingestion: trips, filled-in values and fault scores are all derived from
/// it and can be recomputed when an algorithm changes.
///
/// The commonly used payload fields are lifted into typed columns; the full original
/// payload is kept in <see cref="Payload"/>.
/// </summary>
public class DeviceEvent
{
    public long Id { get; set; }

    public long SourceFileId { get; set; }
    public SourceFile SourceFile { get; set; } = null!;

    /// <summary>1-based line number in the source file.</summary>
    public int LineNumber { get; set; }

    public DateTime Time { get; set; }

    public int VehicleId { get; set; }

    /// <summary>Counting device address; 0 = the on-board computer itself.</summary>
    public int DeviceNumber { get; set; }

    /// <summary>Message code as written in the log (e.g. 11 = counting stopped).</summary>
    public int Code { get; set; }

    public DeviceEventType Type { get; set; }

    /// <summary>Null when the log has no GPS fix (it writes 0,0).</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Context the on-board computer stamps on every line; null when it writes 0.
    public string? BlockCode { get; set; }
    public int? LineId { get; set; }
    public int? LastStopCode { get; set; }
    public int? DelaySeconds { get; set; }

    // Payload fields, typed. Null when the message type doesn't carry them.
    public int? StopCode { get; set; }

    /// <summary>Trip start: the pattern the trip follows (payload <c>id</c>).</summary>
    public int? PatternCode { get; set; }

    public int? Boardings { get; set; }
    public int? Alightings { get; set; }

    /// <summary>Passengers on board after the stop (stop summary only).</summary>
    public int? OnBoard { get; set; }
    public int? OnBoardChange { get; set; }

    /// <summary>Heartbeat: whether the device reports itself alive.</summary>
    public bool? Alive { get; set; }

    /// <summary>Heartbeat: status register, e.g. <c>0x0</c>, <c>0x2</c>.</summary>
    public string? StatusRegister { get; set; }

    public string? FirmwareVersion { get; set; }

    /// <summary>Stop summary: device numbers the vehicle flagged as invalid (<c>chyba</c>).</summary>
    public string? InvalidDevices { get; set; }

    /// <summary>The original key=value payload, untouched.</summary>
    public string? Payload { get; set; }
}

/// <summary>
/// Vendor-neutral event vocabulary. UCP codes map onto it today; EyeOne message types
/// (PC/PO/PP/PI/GP/…) map onto the same values when that parser is added.
/// </summary>
public enum DeviceEventType
{
    Unknown,

    /// <summary>UCP 1 <c>init</c>: number of counting devices (<c>slave_pocet</c>).</summary>
    Init,

    /// <summary>UCP 2 <c>vozidlo</c>: vehicle number, depot, traction, model.</summary>
    VehicleInfo,

    /// <summary>UCP 3 <c>vypnuti systemu</c>.</summary>
    SystemShutdown,

    /// <summary>UCP 5 <c>status</c>: per-device heartbeat (alive, firmware, status register).</summary>
    Heartbeat,

    /// <summary>UCP 7 <c>trasa</c>: trip start — pattern code, first and last stop.</summary>
    TripStart,

    /// <summary>UCP 8 <c>prijezd</c>.</summary>
    Arrival,

    /// <summary>UCP 9 <c>odjezd</c>.</summary>
    Departure,

    /// <summary>UCP 10 <c>start</c>: a device starts counting at a stop.</summary>
    CountingStarted,

    /// <summary>UCP 11 <c>stop</c>: a device stops counting — its in/out for that stop.</summary>
    CountingStopped,

    /// <summary>UCP 12 <c>restart</c>: device restarted; carries counts like a stop.</summary>
    DeviceRestart,

    /// <summary>UCP 15 <c>cestujici</c>: the vehicle's summed in/out and on-board count at a stop.</summary>
    StopSummary,

    /// <summary>UCP 100 <c>dvere</c>: door release/open state.</summary>
    DoorState,

    /// <summary>UCP 110 <c>trasa - faze</c>: trip phase text.</summary>
    TripPhase,

    /// <summary>UCP 120 <c>zastavka - prujezd</c>: stop passed (with delay, speed, GPS).</summary>
    StopPassed,

    /// <summary>UCP 125 <c>zastavka - man.zmena</c>: stop changed manually by the driver.</summary>
    StopChangedManually,

    /// <summary>UCP 200 <c>vozidlo - layout</c>: position in a coupled set, coupled vehicle.</summary>
    VehicleLayout,
}
