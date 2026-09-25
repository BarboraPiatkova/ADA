using AdaPlatform.Domain.Operations;

namespace AdaPlatform.Domain.Quality;

/// <summary>
/// A known problem with a counting device over a time span — either carried over from
/// legacy ADA, or found by the platform's fault detection. ADA: ApcError.
/// </summary>
public class DeviceFault
{
    public long Id { get; set; }

    public int VehicleId { get; set; }

    /// <summary>Null when the fault concerns the whole vehicle's counting system.</summary>
    public int? DeviceNumber { get; set; }

    public long? TripId { get; set; }
    public Trip? Trip { get; set; }

    public DateTime From { get; set; }
    public DateTime? To { get; set; }

    public FaultKind Kind { get; set; }
    public FaultSource Source { get; set; }

    public string? Details { get; set; }
}

public enum FaultKind
{
    // The four kinds ADA distinguishes (its ErrorId 0–3).
    UnexpectedRestart,
    PowerCutUntilEnd,
    PowerCutStatusFalse,
    InvalidPassengerCount,
}

public enum FaultSource
{
    /// <summary>Imported from a legacy ADA database.</summary>
    LegacyAda,

    /// <summary>Found by the platform's fault detection.</summary>
    Detector,
}
