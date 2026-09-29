using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Import.Epcomp;

/// <summary>
/// Takes the stop names as the operator writes them, with diacritics, from EPComp's
/// <c>stations.xml</c> (the operator's stop list, exported with every EPComp package). The vehicles'
/// logs write names without diacritics ("Namesti Miru"), and route ends take the same names.
///
/// Only accents are corrected: a name that differs in more than that (a renamed stop, a different
/// spelling) is reported and kept, because it is the name the vehicles used when the data was recorded.
/// Missing positions are filled; stops the data never saw are not added.
/// </summary>
public sealed partial class EpcompStationsImporter(AppDbContext db)
{
    public async Task<StationsReport> ImportAsync(string path, CancellationToken ct = default)
    {
        var stations = Read(path);
        var byFolded = stations.Values.GroupBy(s => Fold(s.Name))
            .Where(g => g.Select(s => s.Name).Distinct().Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var named = 0;
        var positioned = 0;
        var differing = new List<string>();
        var stops = await db.Stops.ToListAsync(ct);
        foreach (var stop in stops)
        {
            if (!stations.TryGetValue(stop.Code, out var station))
            {
                continue;
            }
            if (Better(stop.Name, station.Name) is { } name)
            {
                stop.Name = name;
                named++;
            }
            else if (stop.Name != station.Name)
            {
                differing.Add($"{stop.Code}: \"{stop.Name}\" / \"{station.Name}\"");
            }
            if (stop.Latitude is null && station.Latitude is not null)
            {
                (stop.Latitude, stop.Longitude, stop.JtskX, stop.JtskY) = (station.Latitude, station.Longitude, station.JtskX, station.JtskY);
                positioned++;
            }
        }

        // Route ends ("Brafova → Komin, smycka") carry the same names, sometimes after the stop code.
        var routes = 0;
        foreach (var pattern in await db.Patterns.ToListAsync(ct))
        {
            var first = Renamed(pattern.FirstStopName, byFolded);
            var last = Renamed(pattern.LastStopName, byFolded);
            if (first != pattern.FirstStopName || last != pattern.LastStopName)
            {
                (pattern.FirstStopName, pattern.LastStopName) = (first, last);
                routes++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new StationsReport(stations.Count, stops.Count(s => stations.ContainsKey(s.Code)), named, positioned, routes, differing);
    }

    /// <summary>The station's name when ours is empty or differs from it only in accents and spacing; else null.</summary>
    public static string? Better(string ours, string theirs) =>
        theirs.Length > 0 && ours != theirs && (ours.Trim().Length == 0 || Fold(ours) == Fold(theirs)) ? theirs : null;

    /// <summary>A route end renamed to the station name it matches (keeping a leading stop code), or unchanged.</summary>
    public static string? Renamed(string? name, IReadOnlyDictionary<string, string> byFolded)
    {
        if (name is null)
        {
            return null;
        }
        var match = LeadingCode().Match(name);
        var (prefix, rest) = match.Success ? (match.Value, name[match.Length..]) : ("", name);
        return byFolded.TryGetValue(Fold(rest), out var proper) && proper != rest ? prefix + proper : name;
    }

    /// <summary>Lower case, no diacritics, single spaces, no space before a comma: "Sokolnice,  Brněnská" → "sokolnice, brnenska".</summary>
    public static string Fold(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                plain.Append(char.ToLowerInvariant(c));
            }
        }
        return Spaces().Replace(plain.ToString().Replace(" ,", ","), " ").Trim();
    }

    private static Dictionary<int, Station> Read(string path)
    {
        var stations = new Dictionary<int, Station>();
        using var reader = XmlReader.Create(path, new XmlReaderSettings { IgnoreComments = true, DtdProcessing = DtdProcessing.Prohibit });
        while (reader.ReadToFollowing("station"))
        {
            if (!int.TryParse(reader.GetAttribute("code"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                continue;
            }
            stations[code] = new Station(
                reader.GetAttribute("name")?.Trim() ?? "",
                Number(reader.GetAttribute("wgs84_lat")), Number(reader.GetAttribute("wgs84_lon")),
                Number(reader.GetAttribute("jtsk_x")), Number(reader.GetAttribute("jtsk_y")));
        }
        return stations.Count > 0 ? stations : throw new InvalidOperationException($"No <station> elements in {path}: is it EPComp's stations.xml?");
    }

    private static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value != 0 ? value : null;

    [GeneratedRegex(@"^\d+\s+")]
    private static partial Regex LeadingCode();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    private sealed record Station(string Name, double? Latitude, double? Longitude, double? JtskX, double? JtskY);
}

/// <param name="Differing">Stops whose name differs in more than accents: kept as they are, listed to check.</param>
public sealed record StationsReport(int Stations, int Matched, int Named, int Positioned, int Routes, IReadOnlyList<string> Differing)
{
    public override string ToString() =>
        $"{Stations} stations in the file, {Matched} of them in the data: {Named} names corrected, {Positioned} positions filled, " +
        $"{Routes} routes renamed. {Differing.Count} names differ in more than accents and were kept" +
        (Differing.Count == 0 ? "." : ":\n  " + string.Join("\n  ", Differing.Take(20)) + (Differing.Count > 20 ? $"\n  … and {Differing.Count - 20} more" : ""));
}
