using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using AdaPlatform.Domain.Operations;
using AdaPlatform.Infrastructure.Fleet;

namespace AdaPlatform.Infrastructure.Import.Transportella;

/// <summary>
/// Where Transportella's per-stop statistics reach the platform from. The platform may run next to
/// Transportella with read access to its statistics database, or receive a dump of that table, or
/// only the XLSX report; each is one implementation, and the import after it is the same.
/// </summary>
public interface ITransportellaStatisticsSource
{
    RecordedCallSource Kind { get; }

    /// <summary>
    /// The records, as <see cref="RecordedCall"/>s without a database id. <paramref name="afterExternalId"/>
    /// lets a source that can filter (the database) skip what an earlier import already has; others
    /// may ignore it, the import skips known ids anyway.
    /// </summary>
    IAsyncEnumerable<RecordedCall> ReadAsync(long afterExternalId, CancellationToken ct = default);
}

/// <summary>
/// A dump of Transportella's <c>Stat.Statistics</c> table: tab-separated rows without a header, in the
/// table's column order, text in code page 852 (DOS Latin 2). Only the operational columns are read;
/// the driver column is skipped and never leaves the file.
/// </summary>
public sealed class TransportellaDumpSource(string path, Encoding? encoding = null) : ITransportellaStatisticsSource
{
    // Column order of Stat.Statistics (TransportellaServer.Data DBStatistics; DutyRoster was added last).
    private const int ColId = 0, ColTripStart = 1, ColVehicle = 2, ColLineCourse = 5, ColTrip = 6,
        ColStation = 8, ColPost = 9, ColPlannedArrival = 12, ColPlannedDeparture = 13,
        ColActualArrival = 14, ColActualDeparture = 15, ColStopName = 27, ColTraction = 33, ColumnCount = 35;
    // Column 3 is the driver: personal data, deliberately not read.

    public RecordedCallSource Kind => RecordedCallSource.TransportellaStatistics;

    public static Encoding DefaultEncoding
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(852);
        }
    }

    public async IAsyncEnumerable<RecordedCall> ReadAsync(long afterExternalId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(path, encoding ?? DefaultEncoding);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (ParseLine(line) is { } call)
            {
                yield return call;
            }
        }
    }

    /// <summary>Null for a line that isn't a statistics row.</summary>
    public static RecordedCall? ParseLine(string line)
    {
        var c = line.Split('\t');
        if (c.Length < ColumnCount - 1
            || !long.TryParse(c[ColId], CultureInfo.InvariantCulture, out var id)
            || TransportellaValues.DateTime(c[ColTripStart]) is not { } tripStart
            || !int.TryParse(c[ColStation], CultureInfo.InvariantCulture, out var station))
        {
            return null;
        }

        var post = short.TryParse(c[ColPost], CultureInfo.InvariantCulture, out var p) ? p : (short)0;
        return TransportellaValues.Call(
            RecordedCallSource.TransportellaStatistics, id, c[ColVehicle], tripStart,
            lineCourse: c[ColLineCourse], line: TransportellaValues.LineFromCourse(c[ColLineCourse]), trip: c[ColTrip],
            station, post, c[ColStopName],
            TransportellaValues.DateTime(c[ColPlannedArrival]), TransportellaValues.DateTime(c[ColPlannedDeparture]),
            TransportellaValues.DateTime(c[ColActualArrival]), TransportellaValues.DateTime(c[ColActualDeparture]),
            c[ColTraction]);
    }
}

/// <summary>Shared value rules for every Transportella source.</summary>
internal static class TransportellaValues
{
    /// <summary>"2026-01-23 13:03:00.0000000"; Transportella writes 0001-01-01 for "not recorded".</summary>
    public static DateTime? DateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("0001-01-01", StringComparison.Ordinal))
        {
            return null;
        }
        return System.DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            ? System.DateTime.SpecifyKind(result, DateTimeKind.Unspecified)
            : null;
    }

    /// <summary>A database value; year 1 means "not recorded".</summary>
    public static DateTime? DateTime(DateTime value) =>
        value.Year <= 1 ? null : System.DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    /// <summary>"18-1-1|18|1" → "18": the line is the part after the first bar.</summary>
    public static string? LineFromCourse(string? lineCourse)
    {
        var parts = (lineCourse ?? "").Split('|');
        return parts.Length >= 2 && parts[1].Trim().Length > 0 ? parts[1].Trim() : null;
    }

    public static RecordedCall Call(
        RecordedCallSource source, long externalId, string vehicleCode, DateTime tripStart,
        string? lineCourse, string? line, string? trip, int station, short post, string? stopName,
        DateTime? plannedArrival, DateTime? plannedDeparture, DateTime? actualArrival, DateTime? actualDeparture,
        string? traction)
    {
        var vehicle = vehicleCode.Trim();
        return new RecordedCall
        {
            Source = source,
            ExternalId = externalId,
            VehicleCode = vehicle,
            VehicleId = int.TryParse(vehicle, CultureInfo.InvariantCulture, out var number) && number > 0 ? number : null,
            TripStart = tripStart,
            Line = Blank(line),
            LineCourse = Blank(lineCourse),
            TripNumber = Blank(trip),
            StationId = station,
            Post = post,
            StopCode = post > 0 ? station * 100 + post : null,
            StopName = Blank(stopName) is { Length: > 255 } tooLong ? tooLong[..255] : Blank(stopName),
            PlannedArrival = plannedArrival,
            PlannedDeparture = plannedDeparture,
            ActualArrival = actualArrival,
            ActualDeparture = actualDeparture,
            Traction = TractionNames.Normalize(traction),
        };
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) || value.Trim() is "-" or "NA" ? null : value.Trim();
}
