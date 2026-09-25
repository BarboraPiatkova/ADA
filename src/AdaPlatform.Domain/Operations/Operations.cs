using AdaPlatform.Domain.Fleet;
using AdaPlatform.Domain.Network;
using AdaPlatform.Domain.Raw;

namespace AdaPlatform.Domain.Operations;

// Derived layer: what happened, reconstructed from raw events (or imported from legacy
// ADA). Every count records whether it was measured or filled in.

/// <summary>One vehicle run along a pattern. ADA: Ride.</summary>
public class Trip
{
    public long Id { get; set; }

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>Second unit of a coupled set, if any.</summary>
    public int? SecondVehicleId { get; set; }
    public Vehicle? SecondVehicle { get; set; }

    public int? PatternCode { get; set; }
    public Pattern? Pattern { get; set; }

    public string? BlockCode { get; set; }
    public Block? Block { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public int InitialDelaySeconds { get; set; }

    public int Boardings { get; set; }
    public int Alightings { get; set; }

    /// <summary>False when a counting device reported an error during the trip.</summary>
    public bool IsValid { get; set; }

    public DataOrigin Origin { get; set; }

    /// <summary>Raw log the trip was reconstructed from; null for trips imported from legacy ADA.</summary>
    public long? SourceFileId { get; set; }
    public SourceFile? SourceFile { get; set; }

    public List<StopVisit> StopVisits { get; set; } = [];
}

/// <summary>What happened at one stop during one trip. ADA: VehicleStopRecord.</summary>
public class StopVisit
{
    public long Id { get; set; }

    public long TripId { get; set; }
    public Trip Trip { get; set; } = null!;

    /// <summary>1-based position within the trip.</summary>
    public int Sequence { get; set; }

    public int StopCode { get; set; }
    public Stop Stop { get; set; } = null!;

    public DateTime? ArrivalTime { get; set; }
    public DateTime? DepartureTime { get; set; }

    /// <summary>Delay against the timetable; negative means early.</summary>
    public int DelaySeconds { get; set; }

    /// <summary>Totals over all doors (the sum of <see cref="DoorCounts"/> when those exist).</summary>
    public int Boardings { get; set; }
    public int Alightings { get; set; }

    /// <summary>
    /// Passengers on board after this stop. Can be negative in real data when counts
    /// drift — kept as measured, not clamped, because that drift is a fault signal.
    /// </summary>
    public int Occupancy { get; set; }

    /// <summary>The vehicle passed the stop without the doors opening.</summary>
    public bool IsPassThrough { get; set; }

    public DataOrigin Origin { get; set; }

    public List<DoorCount> DoorCounts { get; set; } = [];
}

/// <summary>
/// One counting device's in/out at one stop visit. Not available for legacy ADA imports,
/// which only kept totals.
/// </summary>
public class DoorCount
{
    public long StopVisitId { get; set; }
    public StopVisit StopVisit { get; set; } = null!;

    public long CountingDeviceId { get; set; }
    public CountingDevice CountingDevice { get; set; } = null!;

    public int Boardings { get; set; }
    public int Alightings { get; set; }

    /// <summary>The vehicle flagged this device as invalid at this stop (<c>chyba</c>).</summary>
    public bool IsFlaggedInvalid { get; set; }

    public DataOrigin Origin { get; set; }
}

public enum DataOrigin
{
    /// <summary>Reported by a counting device.</summary>
    Measured,

    /// <summary>Entered by hand (ADA: Written).</summary>
    Manual,

    /// <summary>Filled in by a gap-filling method (ADA: Extrapolated).</summary>
    Imputed,
}
