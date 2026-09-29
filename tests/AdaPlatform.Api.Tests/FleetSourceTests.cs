using System.Net;
using System.Text;
using System.Text.Json;
using AdaPlatform.Infrastructure.Fleet;

namespace AdaPlatform.Api.Tests;

/// <summary>Each fleet register adapter maps its source onto the same <see cref="FleetVehicle"/>.</summary>
public class FleetSourceTests
{
    [Fact]
    public void Epis_vehicles_xml_gives_number_type_traction_and_depot()
    {
        // Shape of MDML's EPIS data package (Data/General/vehicles.xml).
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <vehicles creationDate="2026-07-22T11:19:03">
              <vehicle id="38" traction="Bus" tachoConn="none" als="false" type="SOR NB 12 4K40051" depotId="1" biDirect="false"><ccr enabled="true" /></vehicle>
              <vehicle id="58" traction="Trolley" tachoConn="none" als="false" type="SOR 30 TR" depotId="1" biDirect="false" />
              <vehicle id="x" traction="Bus" type="not a vehicle number" />
            </vehicles>
            """;
        var vehicles = EpisVehiclesXmlFleetSource.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        Assert.Equal(
            [new FleetVehicle(38, "SOR NB 12 4K40051", "autobus", "1"), new FleetVehicle(58, "SOR 30 TR", "trolejbus", "1")],
            vehicles);
    }

    [Fact]
    public void Csv_register_reads_named_columns_in_any_order_and_treats_empty_cells_as_unknown()
    {
        const string csv = """
            seating;id;model;standing;excluded
            32;58;SOR 30 TR;62;
            ;38;SOR NB 12;;ano

            ;bad;row;;
            """;
        var vehicles = CsvFleetSource.Parse(new StringReader(csv));

        Assert.Equal(
            [new FleetVehicle(58, "SOR 30 TR", SeatingCapacity: 32, StandingCapacity: 62), new FleetVehicle(38, "SOR NB 12", IsExcluded: true)],
            vehicles);
    }

    [Fact]
    public void A_csv_without_an_id_column_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => CsvFleetSource.Parse(new StringReader("model;seating\nSOR 30 TR;32")));
    }

    [Fact]
    public void Atlas_today_gives_numeric_vehicle_codes_and_traction_ordinals()
    {
        // Atlas's current SyncVehicleTractionDto(Code, Traction), serialised camelCase.
        using var json = JsonDocument.Parse("""[{"code":"58","traction":2},{"code":"38","traction":3},{"code":"SIM-1","traction":7}]""");

        Assert.Equal([new FleetVehicle(58, Traction: "trolejbus"), new FleetVehicle(38, Traction: "autobus")], AtlasFleetSource.Parse(json));
    }

    [Fact]
    public void Atlas_type_and_capacity_are_read_when_an_endpoint_provides_them()
    {
        using var json = JsonDocument.Parse("""[{"Code":"38","Traction":3,"Type":"SOR NB 12","SeatingCapacity":26,"StandingCapacity":76}]""");

        Assert.Equal([new FleetVehicle(38, "SOR NB 12", "autobus", SeatingCapacity: 26, StandingCapacity: 76)], AtlasFleetSource.Parse(json));
    }

    [Fact]
    public async Task Atlas_is_called_with_the_shared_secret_header()
    {
        var handler = new RecordingHandler("""[{"code":"58","traction":2}]""");
        var source = new AtlasFleetSource(new HttpClient(handler), new AtlasFleetOptions
        {
            BaseUrl = "https://atlas.example",
            ApiKeyHeader = "X-Test-Key",
            ApiKey = "secret",
        });

        var vehicles = await source.GetVehiclesAsync();

        Assert.Single(vehicles);
        Assert.Equal("https://atlas.example/api/Integrations/Transportella/VehiclesTraction", handler.Request!.RequestUri!.ToString());
        Assert.Equal("secret", handler.Request.Headers.GetValues("X-Test-Key").Single());
    }

    [Fact]
    public async Task Atlas_without_a_key_is_refused_before_any_call()
    {
        var handler = new RecordingHandler("[]");
        var source = new AtlasFleetSource(new HttpClient(handler), new AtlasFleetOptions { BaseUrl = "https://atlas.example" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => source.GetVehiclesAsync());
        Assert.Null(handler.Request);
    }

    [Theory]
    [InlineData("Bus", "autobus")]
    [InlineData("3", "autobus")]
    [InlineData("TROLLEY", "trolejbus")]
    [InlineData("tramvaj", "tramvaj")]
    [InlineData("7", null)]
    [InlineData("", null)]
    public void Traction_is_stored_in_the_words_the_vehicle_logs_use(string value, string? expected)
    {
        Assert.Equal(expected, TractionNames.Normalize(value));
    }

    private sealed class RecordingHandler(string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
