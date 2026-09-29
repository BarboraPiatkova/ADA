using System.Globalization;
using System.Text.Json;

namespace AdaPlatform.Infrastructure.Fleet;

/// <summary>
/// Pulls the vehicle register from Atlas, following Atlas's own sync contract (docs/transportella-sync.md
/// in the Atlas repo): a GET returning the full list on every call, authenticated by a shared-secret
/// header, upserted by the consumer.
///
/// Atlas's vehicle code is text; only numeric codes (the operator's vehicle numbers) are taken.
/// Today's endpoint returns <c>code</c> and <c>traction</c> (the ordinal of the shared
/// <c>TractionType</c>: 1 tram, 2 trolleybus, 3 bus, …). The optional fields <c>model</c>/<c>type</c>,
/// <c>depot</c>, <c>seatingCapacity</c> and <c>standingCapacity</c> are read too, so a richer Atlas
/// endpoint needs only a configuration change (<see cref="AtlasFleetOptions.VehiclesPath"/>).
/// </summary>
public sealed class AtlasFleetSource(HttpClient http, AtlasFleetOptions options) : IFleetSource
{
    public string Name => "Atlas";

    public async Task<IReadOnlyList<FleetVehicle>> GetVehiclesAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl) || string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.ApiKeyHeader))
        {
            throw new InvalidOperationException(
                "Fleet:Atlas needs BaseUrl, ApiKeyHeader and ApiKey (keep the key in user-secrets or the environment).");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(options.BaseUrl), options.VehiclesPath));
        request.Headers.Add(options.ApiKeyHeader, options.ApiKey);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // The status only; the request carries the key, so it is never logged.
            throw new InvalidOperationException($"Atlas returned {(int)response.StatusCode} for the vehicle list.");
        }

        await using var body = await response.Content.ReadAsStreamAsync(ct);
        return Parse(await JsonDocument.ParseAsync(body, cancellationToken: ct));
    }

    public static IReadOnlyList<FleetVehicle> Parse(JsonDocument json)
    {
        var vehicles = new List<FleetVehicle>();
        foreach (var e in json.RootElement.EnumerateArray())
        {
            if (!int.TryParse(Text(e, "code"), CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }
            vehicles.Add(new FleetVehicle(
                id,
                Model: Text(e, "model") ?? Text(e, "type"),
                Traction: TractionNames.Normalize(Text(e, "traction")),
                Depot: Text(e, "depot"),
                SeatingCapacity: Number(e, "seatingCapacity"),
                StandingCapacity: Number(e, "standingCapacity")));
        }
        return vehicles;
    }

    private static string? Text(JsonElement e, string name)
    {
        if (!TryGet(e, name, out var value))
        {
            return null;
        }
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static int? Number(JsonElement e, string name) =>
        TryGet(e, name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) && n > 0 ? n : null;

    // ASP.NET writes camelCase; accept any casing.
    private static bool TryGet(JsonElement e, string name, out JsonElement value)
    {
        foreach (var property in e.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
}
