namespace AdaPlatform.Domain.Fleet;

public class Vehicle
{
    /// <summary>Operator's vehicle number.</summary>
    public int Id { get; set; }

    public string? Depot { get; set; }
    public string? Traction { get; set; }
    public string? Model { get; set; }

    /// <summary>
    /// Null means unknown. ADA stores 0 for "not filled in", which would make
    /// occupancy % silently divide by zero — imports map 0 to null.
    /// </summary>
    public int? SeatingCapacity { get; set; }
    public int? StandingCapacity { get; set; }

    /// <summary>Excluded from statistics.</summary>
    public bool IsExcluded { get; set; }

    public List<CountingDevice> CountingDevices { get; set; } = [];

    public int? TotalCapacity => SeatingCapacity + StandingCapacity;
}

/// <summary>
/// One passenger-counting unit (UCP-01/UCP-02, or another vendor's sensor), normally one
/// per door. This is the unit that fault detection scores — ADA only kept per-stop totals
/// summed over all doors, so a single failing door was invisible.
/// </summary>
public class CountingDevice
{
    public long Id { get; set; }

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>Device address as the on-board computer reports it (e.g. 41–44, 51–52).</summary>
    public int DeviceNumber { get; set; }

    /// <summary>Last firmware version reported in a heartbeat.</summary>
    public string? FirmwareVersion { get; set; }

    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
