using System.Globalization;
using System.Xml.Linq;

namespace AdaPlatform.Infrastructure.Fleet;

/// <summary>
/// The vehicle list in an EPIS data package (<c>Data/General/vehicles.xml</c>, exported by EPComp):
/// <c>&lt;vehicle id="38" traction="Bus" type="SOR NB 12 4K40051" depotId="1" … /&gt;</c>.
/// It has number, traction, type and depot, but no capacities.
/// </summary>
public sealed class EpisVehiclesXmlFleetSource(string path) : IFleetSource
{
    public string Name => "EPIS vehicles.xml";

    public Task<IReadOnlyList<FleetVehicle>> GetVehiclesAsync(CancellationToken ct = default)
    {
        using var stream = File.OpenRead(path);
        return Task.FromResult(Parse(stream));
    }

    public static IReadOnlyList<FleetVehicle> Parse(Stream xml)
    {
        var vehicles = new List<FleetVehicle>();
        foreach (var e in XDocument.Load(xml).Descendants("vehicle"))
        {
            if (!int.TryParse((string?)e.Attribute("id"), CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }
            vehicles.Add(new FleetVehicle(
                id,
                Model: NullIfBlank((string?)e.Attribute("type")),
                Traction: TractionNames.Normalize((string?)e.Attribute("traction")),
                Depot: NullIfBlank((string?)e.Attribute("depotId"))));
        }
        return vehicles;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// A CSV register, e.g. capacities typed in from the registration papers. A header row names the
/// columns, in any order: <c>id</c> (required), <c>model</c>, <c>traction</c>, <c>depot</c>,
/// <c>seating</c>, <c>standing</c>, <c>excluded</c>. Separated by <c>;</c> (as Czech Excel saves it)
/// or <c>,</c>. An empty cell means "not known".
/// </summary>
public sealed class CsvFleetSource(string path) : IFleetSource
{
    public string Name => "CSV file";

    public Task<IReadOnlyList<FleetVehicle>> GetVehiclesAsync(CancellationToken ct = default)
    {
        using var reader = new StreamReader(path);
        return Task.FromResult(Parse(reader));
    }

    public static IReadOnlyList<FleetVehicle> Parse(TextReader reader)
    {
        var header = reader.ReadLine() ?? throw new InvalidOperationException("The fleet CSV is empty.");
        var separator = header.Contains(';') ? ';' : ',';
        var columns = header.Split(separator).Select(c => c.Trim().TrimStart('﻿').ToLowerInvariant()).ToList();
        var idColumn = columns.IndexOf("id");
        if (idColumn < 0)
        {
            throw new InvalidOperationException("The fleet CSV needs an 'id' column.");
        }

        var vehicles = new List<FleetVehicle>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }
            var cells = line.Split(separator);
            string? Cell(string name)
            {
                var i = columns.IndexOf(name);
                return i >= 0 && i < cells.Length && cells[i].Trim().Length > 0 ? cells[i].Trim() : null;
            }
            if (!int.TryParse(Cell("id"), CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }
            vehicles.Add(new FleetVehicle(
                id,
                Model: Cell("model"),
                Traction: TractionNames.Normalize(Cell("traction")),
                Depot: Cell("depot"),
                SeatingCapacity: Int(Cell("seating")),
                StandingCapacity: Int(Cell("standing")),
                IsExcluded: Cell("excluded")?.ToLowerInvariant() switch
                {
                    null => null,
                    "1" or "true" or "ano" or "yes" => true,
                    _ => false,
                }));
        }
        return vehicles;
    }

    private static int? Int(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : null;
}
