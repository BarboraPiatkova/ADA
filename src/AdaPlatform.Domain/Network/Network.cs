namespace AdaPlatform.Domain.Network;

// Reference data: what the timetable says exists. Names follow standard transit
// terminology (GTFS / Transmodel); ADA's names are noted on each type.
// Keys are the operator's natural codes (CIS stop codes, line numbers), never generated here.

/// <summary>A stop post, keyed by its CIS JŘ code. ADA: Station.</summary>
public class Stop
{
    public int Code { get; set; }
    public required string Name { get; set; }
    public string? Tariffs { get; set; }
    public double? Altitude { get; set; }

    // WGS84 — what the web map uses.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // S-JTSK (EPSG:5514) — what Czech WMS layers use.
    public double? JtskX { get; set; }
    public double? JtskY { get; set; }
}

/// <summary>A public line, keyed by its line number.</summary>
public class Line
{
    public int Id { get; set; }

    public List<Pattern> Patterns { get; set; } = [];
}

/// <summary>
/// One ordered sequence of stops a line's trips follow (a route variant). ADA: Trace.
/// The UCP log's trip-start message (code 7) carries this code as <c>id</c>.
/// </summary>
public class Pattern
{
    public int Code { get; set; }
    public int TargetCode { get; set; }
    public string? FirstStopName { get; set; }
    public string? LastStopName { get; set; }
    public double? LengthKm { get; set; }

    public int LineId { get; set; }
    public Line Line { get; set; } = null!;

    public List<PatternStop> Stops { get; set; } = [];
}

/// <summary>A stop's position on a pattern. ADA: StationOnTrace.</summary>
public class PatternStop
{
    public int PatternCode { get; set; }
    public Pattern Pattern { get; set; } = null!;

    /// <summary>1-based position along the pattern.</summary>
    public int Sequence { get; set; }

    public int StopCode { get; set; }
    public Stop Stop { get; set; } = null!;
}

/// <summary>
/// A vehicle duty — the sequence of trips one vehicle runs ("oběh"). ADA: Service.
/// Kept as text: the UCP log writes it with leading zeros ("01200315").
/// </summary>
public class Block
{
    public required string Code { get; set; }
}
