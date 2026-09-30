using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using AdaPlatform.Domain.Operations;

namespace AdaPlatform.Infrastructure.Import.Transportella;

/// <summary>
/// Transportella's daily service reports ("Vypravenost – detail", exported as <c>OneDayTraffic_&lt;date&gt;</c>):
/// per day and carrier one <c>KurzSpoje_&lt;duty&gt;_&lt;carrier&gt;_&lt;dd-MM-yyyy&gt;.xlsx</c> per duty, with an
/// overview sheet and one sheet per trip (named <c>&lt;line&gt;_&lt;trip&gt;</c>) listing every stop: planned and
/// actual arrival and departure, and the vehicle. Read from a folder or a zip of such folders.
///
/// Stops are named, not numbered: <see cref="RecordedCall.StationId"/> stays 0 here and the import
/// matches the name to the operator's stop list. Only the chosen carrier's duties are read when
/// <paramref name="carrier"/> is set (a deployment serves one operator); without it every carrier's,
/// with the carrier number in front of the vehicle, because fleet numbers repeat between carriers.
/// </summary>
public sealed partial class TransportellaDailyServiceSource(string path, string? carrier = null) : ITransportellaStatisticsSource
{
    public RecordedCallSource Kind => RecordedCallSource.TransportellaDailyService;

    public async IAsyncEnumerable<RecordedCall> ReadAsync(long afterExternalId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var (name, open) in Files())
        {
            ct.ThrowIfCancellationRequested();
            if (DutyFile().Match(name) is not { Success: true } m
                || !DateOnly.TryParseExact(m.Groups["day"].Value, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                continue;
            }
            using var buffer = new MemoryStream();
            await using (var stream = open())
            {
                await stream.CopyToAsync(buffer, ct);
            }
            buffer.Position = 0;
            foreach (var call in ParseWorkbook(buffer, day, m.Groups["duty"].Value, int.Parse(m.Groups["carrier"].Value, CultureInfo.InvariantCulture), carrier))
            {
                yield return call;
            }
        }
    }

    /// <summary>The files of the folder, or of the zip: name and how to open it.</summary>
    private IEnumerable<(string Name, Func<Stream> Open)> Files()
    {
        if (Directory.Exists(path))
        {
            foreach (var file in Directory.EnumerateFiles(path, "KurzSpoje_*.xlsx", SearchOption.AllDirectories).Order())
            {
                yield return (Path.GetFileName(file), () => File.OpenRead(file));
            }
            yield break;
        }
        using var archive = ZipFile.OpenRead(path);
        foreach (var entry in archive.Entries.Where(e => e.Name.StartsWith("KurzSpoje_", StringComparison.Ordinal)).OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            yield return (entry.Name, entry.Open);
        }
    }

    /// <summary>
    /// The calls of one duty's workbook. Null carrier name = every carrier; otherwise only a workbook whose
    /// overview names that carrier (a part of the name is enough, e.g. "Dopravní podnik města Brna").
    /// </summary>
    public static IEnumerable<RecordedCall> ParseWorkbook(Stream xlsx, DateOnly day, string duty, int carrierNumber, string? carrier)
    {
        using var archive = new ZipArchive(xlsx, ZipArchiveMode.Read, leaveOpen: true);
        var strings = SharedStrings(archive);
        var sheets = SheetNames(archive);

        // The overview (first sheet) names the carrier on its second row.
        if (carrier is not null)
        {
            var overview = sheets.FirstOrDefault();
            var name = overview.Part is null ? null : Rows(archive, overview.Part, strings).Skip(1).FirstOrDefault()?.GetValueOrDefault(0);
            if (name is null || !name.Contains(carrier, StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }
        }

        foreach (var (sheetName, part) in sheets)
        {
            if (TripSheet().Match(sheetName) is not { Success: true } trip)
            {
                continue;
            }
            foreach (var call in ParseTrip(Rows(archive, part, strings), day, duty, trip.Groups["line"].Value, trip.Groups["trip"].Value, carrier is null ? carrierNumber : null))
            {
                yield return call;
            }
        }
    }

    /// <summary>
    /// One trip sheet: the stop rows after the header row starting "zastávka". Columns: A stop, B/C planned
    /// arrival/departure ("05:58"), D/E actual arrival/departure ("05:56:24"), H vehicle.
    /// </summary>
    public static IEnumerable<RecordedCall> ParseTrip(IReadOnlyList<Dictionary<int, string>> rows, DateOnly day, string duty, string line, string trip, int? carrierNumber)
    {
        var header = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].GetValueOrDefault(0)?.Trim() == "zastávka")
            {
                header = i;
                break;
            }
        }
        if (header < 0)
        {
            yield break;
        }

        var stops = rows.Skip(header + 1).Where(r => r.GetValueOrDefault(0) is { Length: > 0 }).ToList();
        // The trip starts at its first planned time; later times past midnight roll over to the next day.
        var first = stops.Select(r => Clock(r.GetValueOrDefault(2)) ?? Clock(r.GetValueOrDefault(1))).FirstOrDefault(t => t is not null);
        if (first is not { } firstTime)
        {
            yield break;
        }
        var tripStart = day.ToDateTime(TimeOnly.MinValue) + firstTime;
        DateTime? At(string? text) => Clock(text) is { } time
            ? (day.ToDateTime(TimeOnly.MinValue) + time) is var at && at < tripStart.AddHours(-6) ? at.AddDays(1) : at
            : null;

        var position = 0;
        foreach (var r in stops)
        {
            position++;
            var stopName = r[0].Trim();
            var vehicle = r.GetValueOrDefault(7)?.Trim() is { Length: > 0 } v && v != "0" ? v : "";
            var code = carrierNumber is { } n && vehicle.Length > 0 ? $"{n}:{vehicle}" : vehicle;
            yield return TransportellaValues.Call(
                RecordedCallSource.TransportellaDailyService, StableId(day, duty, line, trip, position, stopName),
                code, tripStart, lineCourse: duty, line, trip, station: 0, post: 0, stopName,
                At(r.GetValueOrDefault(1)), At(r.GetValueOrDefault(2)), At(r.GetValueOrDefault(3)), At(r.GetValueOrDefault(4)),
                traction: null);
        }
    }

    /// <summary>"05:58" or "05:56:24"; hours past 23 (a trip after midnight) are allowed.</summary>
    private static TimeSpan? Clock(string? text)
    {
        if (text is null || ClockText().Match(text.Trim()) is not { Success: true } m)
        {
            return null;
        }
        var seconds = m.Groups["s"].Success ? int.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture) : 0;
        return new TimeSpan(int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture), seconds);
    }

    private static long StableId(DateOnly day, string duty, string line, string trip, int position, string stop)
    {
        var key = $"{day:yyyy-MM-dd}|{duty}|{line}/{trip}|{position}|{stop}";
        return BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(key))) & long.MaxValue;
    }

    private static List<string> SharedStrings(ZipArchive archive)
    {
        var strings = new List<string>();
        if (archive.GetEntry("xl/sharedStrings.xml") is not { } entry)
        {
            return strings;
        }
        using var reader = XmlReader.Create(entry.Open(), new XmlReaderSettings { IgnoreWhitespace = true });
        StringBuilder? text = null;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si")
            {
                text = new StringBuilder();
            }
            else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t" && text is not null)
            {
                text.Append(reader.ReadElementContentAsString());
                if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "si")
                {
                    strings.Add(text.ToString());
                    text = null;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "si" && text is not null)
            {
                strings.Add(text.ToString());
                text = null;
            }
        }
        return strings;
    }

    /// <summary>The workbook's sheets in order: name and the zip part holding it.</summary>
    private static List<(string Name, string Part)> SheetNames(ZipArchive archive)
    {
        var targets = new Dictionary<string, string>();
        if (archive.GetEntry("xl/_rels/workbook.xml.rels") is { } rels)
        {
            using var reader = XmlReader.Create(rels.Open());
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship" && reader.GetAttribute("Id") is { } id && reader.GetAttribute("Target") is { } target)
                {
                    targets[id] = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
                }
            }
        }
        var sheets = new List<(string, string)>();
        if (archive.GetEntry("xl/workbook.xml") is { } workbook)
        {
            using var reader = XmlReader.Create(workbook.Open());
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheet"
                    && reader.GetAttribute("name") is { } name
                    && reader.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships") is { } rid
                    && targets.TryGetValue(rid, out var part))
                {
                    sheets.Add((name, part));
                }
            }
        }
        return sheets;
    }

    /// <summary>A sheet's rows, each as column index (A = 0) → text; shared strings resolved.</summary>
    private static List<Dictionary<int, string>> Rows(ZipArchive archive, string part, List<string> strings)
    {
        var rows = new List<Dictionary<int, string>>();
        if (archive.GetEntry(part) is not { } entry)
        {
            return rows;
        }
        using var reader = XmlReader.Create(entry.Open(), new XmlReaderSettings { IgnoreWhitespace = true });
        Dictionary<int, string>? row = null;
        int column = -1;
        string? type = null;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.LocalName)
                {
                    case "row":
                        row = [];
                        rows.Add(row);
                        break;
                    case "c":
                        column = ColumnIndex(reader.GetAttribute("r"));
                        type = reader.GetAttribute("t");
                        break;
                    case "v" when row is not null && column >= 0:
                        var value = reader.ReadElementContentAsString();
                        row[column] = type == "s" && int.TryParse(value, CultureInfo.InvariantCulture, out var index) && index < strings.Count ? strings[index] : value;
                        break;
                    case "t" when row is not null && column >= 0 && type == "inlineStr":
                        row[column] = reader.ReadElementContentAsString();
                        break;
                }
            }
        }
        return rows;
    }

    /// <summary>"D7" → 3.</summary>
    private static int ColumnIndex(string? reference)
    {
        if (reference is null)
        {
            return -1;
        }
        var index = 0;
        foreach (var c in reference)
        {
            if (c is < 'A' or > 'Z')
            {
                break;
            }
            index = index * 26 + (c - 'A' + 1);
        }
        return index - 1;
    }

    [GeneratedRegex(@"^KurzSpoje_(?<duty>[^_]+)_(?<carrier>\d+)_(?<day>\d{2}-\d{2}-\d{4})\.xlsx$")]
    private static partial Regex DutyFile();

    [GeneratedRegex(@"^(?<line>[^_]+)_(?<trip>\d+)$")]
    private static partial Regex TripSheet();

    [GeneratedRegex(@"^(?<h>\d{1,2}):(?<m>\d{2})(:(?<s>\d{2}))?$")]
    private static partial Regex ClockText();
}
