namespace AdaPlatform.Domain.Operations;

/// <summary>
/// One vehicle's call at one stop as the operator's dispatch system (Transportella) recorded it:
/// planned and actual arrival and departure. Operations only: passengers come from the counting
/// units (<see cref="StopVisit"/>), and the two are joined per vehicle, stop and time. SIRI: RecordedCall.
/// No driver is stored; the platform doesn't need personal data.
/// </summary>
public class RecordedCall
{
    public long Id { get; set; }

    public RecordedCallSource Source { get; set; }

    /// <summary>
    /// Identity within the source: Transportella's own statistics row id, or for a report import a
    /// stable hash of the row's natural key. Makes re-imports skip what is already there.
    /// </summary>
    public long ExternalId { get; set; }

    /// <summary>The vehicle as recorded: its fleet number, or a registration plate in regional systems.</summary>
    public required string VehicleCode { get; set; }

    /// <summary>The fleet number when <see cref="VehicleCode"/> is one; joins to the counting data.</summary>
    public int? VehicleId { get; set; }

    /// <summary>Start of the trip the call belongs to (Transportella: vehicle–line connection timestamp).</summary>
    public DateTime TripStart { get; set; }

    public string? Line { get; set; }

    /// <summary>Line, course and duty as the source writes them, e.g. <c>18-1-1|18|1</c>.</summary>
    public string? LineCourse { get; set; }

    public string? TripNumber { get; set; }

    /// <summary>Station number from the operator's timetable (EPComp).</summary>
    public int StationId { get; set; }

    /// <summary>Stop post within the station; 0 when the source doesn't record it.</summary>
    public short Post { get; set; }

    /// <summary>
    /// The post code the vehicles' logs use (station × 100 + post), or null when the post is unknown;
    /// then only the station can be matched.
    /// </summary>
    public int? StopCode { get; set; }

    public string? StopName { get; set; }

    public DateTime? PlannedArrival { get; set; }
    public DateTime? PlannedDeparture { get; set; }
    public DateTime? ActualArrival { get; set; }
    public DateTime? ActualDeparture { get; set; }

    public string? Traction { get; set; }
}

public enum RecordedCallSource
{
    /// <summary>Transportella's statistics table (read directly or from a dump of it).</summary>
    TransportellaStatistics,

    /// <summary>Transportella's per-trip statistics report exported as XLSX.</summary>
    TransportellaReport,

    /// <summary>Transportella's daily service reports ("Vypravenost – detail", OneDayTraffic), one workbook per duty.</summary>
    TransportellaDailyService,
}
