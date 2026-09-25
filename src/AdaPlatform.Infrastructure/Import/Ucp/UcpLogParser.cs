using System.Globalization;
using System.Text.RegularExpressions;
using AdaPlatform.Domain.Raw;

namespace AdaPlatform.Infrastructure.Import.Ucp;

/// <summary>
/// Parses the per-vehicle daily log the on-board computer writes for UCP-01/UCP-02
/// counting units (<c>APC_&lt;vehicle&gt;.&lt;yyyy-MM-dd&gt;.csv</c>).
///
/// Each line has 12 semicolon-separated columns:
/// <code>
///  0 time          yyyy-MM-dd HH:mm:ss (local)
///  1 GPS           "lat , lon"; "0.000000 , 0.000000" = no fix
///  2 block         e.g. 01200315; 0 = none
///  3 line          0 = none
///  4 last stop     CIS code; 0 = none
///  5 delay         ±mm:ss (minutes may exceed 59)
///  6 message code  see <see cref="DeviceEventType"/>
///  7 description   human-readable name of the code
///  8 device        counting device address; 0 = on-board computer
///  9 payload       key=value, comma-separated
/// 10 on board      stop summary only
/// 11 change        stop summary only
/// </code>
/// The format was reverse-engineered from ADA's parser (ADA.Data APCDataParser) and checked
/// against one week of real logs; there is no public specification.
/// </summary>
public static partial class UcpLogParser
{
    private const int ColumnCount = 12;

    [GeneratedRegex(@"^APC_(?<vehicle>\d+)\.(?<date>\d{4}-\d{2}-\d{2})\.csv$", RegexOptions.IgnoreCase)]
    private static partial Regex FileNamePattern();

    // A payload key: start of payload or after a comma, then an identifier and '='.
    // Values may themselves contain commas ("gps=[49.19 , 16.57]"), so values are
    // everything up to the next key, not up to the next comma.
    [GeneratedRegex(@"(?:^|,)\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)=")]
    private static partial Regex PayloadKey();

    public static bool TryParseFileName(string fileName, out int vehicleId, out DateOnly serviceDate)
    {
        vehicleId = 0;
        serviceDate = default;
        var match = FileNamePattern().Match(fileName);
        return match.Success
            && int.TryParse(match.Groups["vehicle"].Value, CultureInfo.InvariantCulture, out vehicleId)
            && DateOnly.TryParseExact(match.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out serviceDate);
    }

    public static UcpParseResult Parse(TextReader reader, int vehicleId)
    {
        var result = new UcpParseResult();
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Length == 0)
            {
                continue;
            }

            var deviceEvent = ParseLine(line, lineNumber, vehicleId);
            if (deviceEvent is null)
            {
                result.MalformedLineCount++;
                continue;
            }

            result.Events.Add(deviceEvent);
            if (deviceEvent.Type == DeviceEventType.VehicleInfo && result.VehicleInfo is null)
            {
                result.VehicleInfo = ParsePayload(deviceEvent.Payload);
            }
        }

        result.LineCount = lineNumber;
        return result;
    }

    /// <summary>Returns null for a line that doesn't have the expected shape.</summary>
    public static DeviceEvent? ParseLine(string line, int lineNumber, int vehicleId)
    {
        var columns = line.Split(';');
        if (columns.Length != ColumnCount
            || !DateTime.TryParseExact(columns[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            || !int.TryParse(columns[6], CultureInfo.InvariantCulture, out var code)
            || !int.TryParse(columns[8], CultureInfo.InvariantCulture, out var device))
        {
            return null;
        }

        var type = MapCode(code);
        var payload = ParsePayload(columns[9]);
        var (latitude, longitude) = ParseGps(columns[1]);

        return new DeviceEvent
        {
            LineNumber = lineNumber,
            Time = DateTime.SpecifyKind(time, DateTimeKind.Unspecified),
            VehicleId = vehicleId,
            DeviceNumber = device,
            Code = code,
            Type = type,
            Latitude = latitude,
            Longitude = longitude,
            BlockCode = columns[2] is "" or "0" ? null : columns[2],
            LineId = PositiveInt(columns[3]),
            LastStopCode = PositiveInt(columns[4]),
            DelaySeconds = ParseDelay(columns[5]),
            StopCode = type is DeviceEventType.StopPassed or DeviceEventType.StopChangedManually
                ? PositiveInt(payload.GetValueOrDefault("id"))
                : PositiveInt(payload.GetValueOrDefault("zastavka")),
            PatternCode = type == DeviceEventType.TripStart ? PositiveInt(payload.GetValueOrDefault("id")) : null,
            Boardings = Int(payload.GetValueOrDefault("in")),
            Alightings = Int(payload.GetValueOrDefault("out")),
            OnBoard = Int(columns[10]),
            OnBoardChange = Int(columns[11]),
            Alive = payload.GetValueOrDefault("alive") switch { "true" => true, "false" => false, _ => null },
            StatusRegister = payload.GetValueOrDefault("statusReg"),
            FirmwareVersion = payload.GetValueOrDefault("version"),
            InvalidDevices = payload.GetValueOrDefault("chyba")?.Trim('[', ']', ' '),
            Payload = columns[9].Length == 0 ? null : columns[9],
        };
    }

    public static DeviceEventType MapCode(int code) => code switch
    {
        1 => DeviceEventType.Init,
        2 => DeviceEventType.VehicleInfo,
        3 => DeviceEventType.SystemShutdown,
        5 => DeviceEventType.Heartbeat,
        7 => DeviceEventType.TripStart,
        8 => DeviceEventType.Arrival,
        9 => DeviceEventType.Departure,
        10 => DeviceEventType.CountingStarted,
        11 => DeviceEventType.CountingStopped,
        12 => DeviceEventType.DeviceRestart,
        15 => DeviceEventType.StopSummary,
        100 => DeviceEventType.DoorState,
        110 => DeviceEventType.TripPhase,
        120 => DeviceEventType.StopPassed,
        125 => DeviceEventType.StopChangedManually,
        200 => DeviceEventType.VehicleLayout,
        _ => DeviceEventType.Unknown,
    };

    public static Dictionary<string, string> ParsePayload(string? payload)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(payload))
        {
            return result;
        }

        var keys = PayloadKey().Matches(payload);
        for (var i = 0; i < keys.Count; i++)
        {
            var valueStart = keys[i].Index + keys[i].Length;
            var valueEnd = i + 1 < keys.Count ? keys[i + 1].Index : payload.Length;
            var value = payload[valueStart..valueEnd].Trim().Trim('"');
            result.TryAdd(keys[i].Groups["key"].Value, value);
        }
        return result;
    }

    /// <summary>"+01:30" → 90, "-00:56" → -56. Null if unparseable.</summary>
    public static int? ParseDelay(string value)
    {
        if (value.Length < 4 || value[0] is not ('+' or '-'))
        {
            return null;
        }

        var parts = value[1..].Split(':');
        if (parts.Length != 2
            || !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var minutes)
            || !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var seconds))
        {
            return null;
        }

        var total = minutes * 60 + seconds;
        return value[0] == '-' ? -total : total;
    }

    private static (double?, double?) ParseGps(string value)
    {
        var parts = value.Split(',');
        if (parts.Length != 2
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            || (latitude == 0 && longitude == 0))
        {
            return (null, null);
        }
        return (latitude, longitude);
    }

    private static int? Int(string? value) =>
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result) ? result : null;

    private static int? PositiveInt(string? value) => Int(value) is > 0 and var result ? result : null;
}

public sealed class UcpParseResult
{
    public List<DeviceEvent> Events { get; } = [];
    public int LineCount { get; set; }
    public int MalformedLineCount { get; set; }

    /// <summary>Payload of the first vehicle-info message (cislo, vozovna, trakce, typ), if any.</summary>
    public Dictionary<string, string>? VehicleInfo { get; set; }
}
