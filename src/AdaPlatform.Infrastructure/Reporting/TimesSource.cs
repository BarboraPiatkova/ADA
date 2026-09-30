namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// Where the operations screens take arrival and departure times from: the vehicles' own logs (with
/// passengers from their counting units), or the dispatch system Transportella (times only).
/// </summary>
public enum TimesSource
{
    VehicleLog,
    Transportella,
}
