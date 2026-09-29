namespace AdaPlatform.Infrastructure.Fleet;

/// <summary>
/// One vehicle as a fleet register describes it. Null means "this source doesn't say", never
/// "clear it": a sync only writes the values a source actually provides.
/// </summary>
public sealed record FleetVehicle(
    int Id,
    string? Model = null,
    string? Traction = null,
    string? Depot = null,
    int? SeatingCapacity = null,
    int? StandingCapacity = null,
    bool? IsExcluded = null);

/// <summary>
/// Where the operator's vehicle register comes from: Atlas, the EPIS data package, a file, or
/// whatever an operator uses next. Adding a source means adding one implementation and one
/// <see cref="FleetSourceKind"/> value; <see cref="FleetSync"/> and everything after it stay the same.
/// </summary>
public interface IFleetSource
{
    /// <summary>Shown in the sync report, e.g. "Atlas" or "EPIS vehicles.xml".</summary>
    string Name { get; }

    /// <summary>The full register, every call (a snapshot, not a change list).</summary>
    Task<IReadOnlyList<FleetVehicle>> GetVehiclesAsync(CancellationToken ct = default);
}

public enum FleetSourceKind
{
    /// <summary>No register: vehicles come only from the logs; type capacities still apply.</summary>
    None,

    /// <summary>Atlas, Herman's register of vehicles and on-board devices (HTTP pull).</summary>
    Atlas,

    /// <summary>The <c>vehicles.xml</c> of an EPIS data package (from EPComp).</summary>
    EpisVehiclesXml,

    /// <summary>A CSV file, e.g. capacities typed in from the vehicles' registration papers.</summary>
    Csv,
}

/// <summary>Settings a deployment sets in the "Fleet" section.</summary>
public sealed record FleetOptions
{
    public const string SectionName = "Fleet";

    public FleetSourceKind Source { get; init; } = FleetSourceKind.None;

    /// <summary>File for <see cref="FleetSourceKind.EpisVehiclesXml"/> and <see cref="FleetSourceKind.Csv"/>.</summary>
    public string? Path { get; init; }

    public AtlasFleetOptions Atlas { get; init; } = new();

    /// <summary>
    /// Seated and standing capacity per vehicle type (model name as the register or the log writes
    /// it). Fills a vehicle's capacity only where no source knows it, because the same type seats
    /// differently depending on its door and seat layout.
    /// </summary>
    public Dictionary<string, TypeCapacity> TypeCapacities { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record TypeCapacity(int Seating, int Standing);

public sealed record AtlasFleetOptions
{
    /// <summary>Atlas server, e.g. the operator's internal address. Secrets and addresses stay in configuration.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Today Atlas exposes only vehicle numbers and traction (its Transportella sync endpoint).
    /// A dedicated endpoint with type and capacity can replace it without code changes here: the
    /// adapter reads those fields when they are present.
    /// </summary>
    public string VehiclesPath { get; init; } = "/api/Integrations/Transportella/VehiclesTraction";

    /// <summary>Shared-secret header Atlas checks for server-to-server calls.</summary>
    public string? ApiKeyHeader { get; init; }

    public string? ApiKey { get; init; }
}

/// <summary>
/// Traction as the platform stores it: the words the vehicles' own logs use (<c>tramvaj</c>,
/// <c>trolejbus</c>, <c>autobus</c>), so vehicles from a register and from the logs group together.
/// </summary>
public static class TractionNames
{
    public static string? Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "1" or "tram" or "tramvaj" => "tramvaj",
        "2" or "trolley" or "trolleybus" or "trolejbus" => "trolejbus",
        "3" or "bus" or "autobus" => "autobus",
        "4" or "technical_vehicle" => "technické vozidlo",
        "5" or "ship" or "lod" or "loď" => "loď",
        "6" or "train" or "vlak" => "vlak",
        "7" or "unknown" => null,
        var other => other,
    };
}
