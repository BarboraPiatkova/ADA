using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using AdaPlatform.Domain.Operations;

namespace AdaPlatform.Infrastructure.Import.Transportella;

/// <summary>
/// Transportella's per-trip statistics report exported as XLSX (<c>StaLineCourse_*.xlsx</c>): an overview
/// sheet, then one sheet per trip with a header ("Detail spoje", "Datum a čas zahájení") and one row per
/// stop. Cells carry an empty <c>r=""</c> attribute, so they are read by position, not by reference.
///
/// Only operations are taken (planned and actual times). The report also has APC columns; passengers
/// come from the counting units' own logs, which the platform can check, so these are left out.
/// Rows have no id of their own; <see cref="RecordedCall.ExternalId"/> is a hash of trip, vehicle,
/// position and stop, so importing the same report twice adds nothing.
/// </summary>
public sealed class TransportellaReportXlsxSource(string path) : ITransportellaStatisticsSource
{
    // Stop table columns (row after the "Kód zastávky" header).
    private const int ColStation = 0, ColPostCode = 2 /* "Název" */, ColPlannedArrival = 5, ColPlannedDeparture = 6,
        ColActualArrival = 7, ColActualDeparture = 8, ColVehicle = 25, ColVehicleType = 26;

    public RecordedCallSource Kind => RecordedCallSource.TransportellaReport;

    public async IAsyncEnumerable<RecordedCall> ReadAsync(long afterExternalId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var archive = ZipFile.OpenRead(path);
        // Transportella writes the overview sheet as an empty (0-byte) part.
        foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal)
                                                          && e.Name.EndsWith(".xml", StringComparison.Ordinal) && e.Length > 0))
        {
            ct.ThrowIfCancellationRequested();
            await using var stream = entry.Open();
            foreach (var call in ParseTripSheet(ReadRows(stream)))
            {
                yield return call;
            }
        }
    }

    /// <summary>The cell texts of each row, in order.</summary>
    public static List<List<string>> ReadRows(Stream sheetXml)
    {
        var rows = new List<List<string>>();
        using var reader = XmlReader.Create(sheetXml, new XmlReaderSettings { IgnoreWhitespace = true });
        List<string>? row = null;
        StringBuilder? cell = null;
        var more = reader.Read();
        while (more)
        {
            // ReadElementContentAsString already moves to the next node; don't read past it.
            var advanced = false;
            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.LocalName)
                {
                    case "row":
                        row = [];
                        rows.Add(row);
                        if (reader.IsEmptyElement) row = null;
                        break;
                    case "c":
                        if (reader.IsEmptyElement) row?.Add("");
                        else cell = new StringBuilder();
                        break;
                    case "v" or "t" when cell is not null:
                        cell.Append(reader.ReadElementContentAsString());
                        advanced = true;
                        break;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "c" && cell is not null)
            {
                row?.Add(cell.ToString());
                cell = null;
            }
            more = advanced ? !reader.EOF : reader.Read();
        }
        return rows;
    }

    /// <summary>The stop rows of one trip sheet; nothing for the overview or an unrecognised sheet.</summary>
    public static IEnumerable<RecordedCall> ParseTripSheet(List<List<string>> rows)
    {
        string? Header(params string[] labels) => rows.FirstOrDefault(r => r.Count > 0 && labels.Contains(r[0].Trim()))
            ?.Skip(1).FirstOrDefault(v => v.Trim().Length > 0)?.Trim();

        // "100305/4002": line / trip ("Detail vlaku: 7841" for a train). "09/05/2026 08:33:35": month/day/year.
        if (Header("Detail spoje:", "Detail vlaku:") is not { } tripKey
            || !DateTime.TryParseExact(Header("Datum a čas zahájení:"), "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tripStart))
        {
            yield break;
        }
        var (line, trip) = tripKey.Split('/') is [var l, var t] ? (l, t) : (tripKey, (string?)null);

        var table = rows.FindIndex(r => r.Count > 0 && r[0].Trim() == "Kód zastávky");
        if (table < 0)
        {
            yield break;
        }

        var position = 0;
        foreach (var r in rows.Skip(table + 1))
        {
            if (r.Count <= ColActualDeparture || !int.TryParse(r[ColStation], CultureInfo.InvariantCulture, out var station))
            {
                continue;
            }
            position++;
            var vehicle = Cell(r, ColVehicle) is { } v && v is not ("NA" or "-") ? v : "";
            yield return TransportellaValues.Call(
                RecordedCallSource.TransportellaReport, StableId(tripKey, tripStart, vehicle, position, station),
                vehicle, tripStart, lineCourse: tripKey, line, trip, station, Post(r, station), StopName(r),
                Time(r, ColPlannedArrival, tripStart), Time(r, ColPlannedDeparture, tripStart),
                Time(r, ColActualArrival, tripStart), Time(r, ColActualDeparture, tripStart),
                traction: Cell(r, ColVehicleType) switch
                {
                    "Autobus" => "autobus",
                    "Vlak" => "vlak",
                    "Trolejbus" => "trolejbus",
                    "Tramvaj" => "tramvaj",
                    _ => null,
                });
        }
    }

    // The "Název" column holds the stop name, or in some reports the post code instead.
    private static string? StopName(List<string> row) =>
        Cell(row, ColPostCode) is { } value && !value.All(char.IsDigit) ? value.Replace(",,", ", ") : null;

    // When the "Název" column holds a post code, it is station × 100 + post, e.g. 1403801 for station 14038.
    private static short Post(List<string> row, int station) =>
        long.TryParse(Cell(row, ColPostCode), CultureInfo.InvariantCulture, out var code) && code / 100 == station
            ? (short)(code % 100)
            : (short)0;

    /// <summary>"08:39:42" on the trip's day; a time well before the trip start is after midnight.</summary>
    private static DateTime? Time(List<string> row, int column, DateTime tripStart)
    {
        if (!TimeSpan.TryParseExact(Cell(row, column), @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var time))
        {
            return null;
        }
        var at = tripStart.Date + time;
        return at < tripStart.AddHours(-6) ? at.AddDays(1) : at;
    }

    private static string? Cell(List<string> row, int column) =>
        column < row.Count && row[column].Trim() is { Length: > 0 } value ? value : null;

    private static long StableId(string tripKey, DateTime tripStart, string vehicle, int position, int station)
    {
        var key = $"{tripKey}|{tripStart:yyyy-MM-ddTHH:mm:ss}|{vehicle}|{position}|{station}";
        return BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(key))) & long.MaxValue;
    }
}
