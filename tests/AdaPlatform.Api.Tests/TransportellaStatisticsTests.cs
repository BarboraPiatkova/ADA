using System.Text;
using AdaPlatform.Api.Tests.Infrastructure;
using AdaPlatform.Domain.Operations;
using AdaPlatform.Infrastructure.Import.Transportella;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdaPlatform.Api.Tests;

/// <summary>
/// Transportella's per-stop statistics, from each way they can arrive. Rows are shaped like DPMO's
/// statistics dump (Olomouc, January 2026); the driver number is replaced (4321) and must never appear
/// in the result.
/// </summary>
public class TransportellaStatisticsTests
{
    // ID, trip start, vehicle, DRIVER, speed, line|course, trip, delay, station, post, lon, lat,
    // planned arr/dep, actual arr/dep, APC ×3, distances ×2, stopped, predicted, temperature,
    // electricity, ride type, tariff, stop name, stop type, ticketing ×4, traction, duty roster.
    public const string DumpLine =
        "2397366\t2026-01-23 13:03:00.0000000\t1665\t4321\t0\t18-1-1|18|1\t15\t-2\t104\t0\t0.0\t0.0\t" +
        "2026-01-23 13:15:00.0000000\t2026-01-23 13:15:00.0000000\t2026-01-23 13:15:29.0000000\t2026-01-23 13:17:41.0000000\t" +
        "0\t0\t0\t0.0\t0.0\t0\t\t0\t0.0\t0\t0\tLadova\t0\t0\t0\t0\t0\t7\t";

    [Fact]
    public void A_dump_row_gives_the_operational_columns_and_no_driver()
    {
        var call = TransportellaDumpSource.ParseLine(DumpLine)!;

        Assert.Equal((2397366L, "1665", 1665, "18", "18-1-1|18|1", "15"), (call.ExternalId, call.VehicleCode, call.VehicleId, call.Line, call.LineCourse, call.TripNumber));
        Assert.Equal((104, (short)0, (int?)null, "Ladova"), (call.StationId, call.Post, call.StopCode, call.StopName));
        Assert.Equal(new DateTime(2026, 1, 23, 13, 15, 0), call.PlannedDeparture);
        Assert.Equal(new DateTime(2026, 1, 23, 13, 15, 29), call.ActualArrival);
        Assert.Equal(new DateTime(2026, 1, 23, 13, 17, 41), call.ActualDeparture);
        Assert.Null(call.Traction);   // 7 = unknown
        Assert.DoesNotContain("4321", System.Text.Json.JsonSerializer.Serialize(call));
    }

    [Fact]
    public void Not_recorded_times_are_null_and_malformed_lines_are_skipped()
    {
        var line = DumpLine.Replace("2026-01-23 13:15:29.0000000", "0001-01-01 00:00:00.0000000");
        Assert.Null(TransportellaDumpSource.ParseLine(line)!.ActualArrival);
        Assert.Null(TransportellaDumpSource.ParseLine("not\ta\trow"));
    }

    [Fact]
    public void Dumps_are_read_as_code_page_852()
    {
        // "Řepčín, železárny" as the dump stores it.
        var bytes = TransportellaDumpSource.DefaultEncoding.GetBytes("Řepčín, železárny");
        Assert.Equal([0xFC, 0x65, 0x70, 0x9F], bytes.Take(4));
    }

    [Fact]
    public void Report_cells_are_read_by_position_although_their_reference_is_empty()
    {
        const string sheet = """
            <x:worksheet xmlns:x="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><x:sheetData>
            <x:row><x:c r="" t="str"><x:v>Detail spoje:</x:v></x:c><x:c r="" t="str"><x:v></x:v></x:c><x:c r="" t="str"><x:v>100305/4002</x:v></x:c></x:row>
            <x:row><x:c r="" /><x:c r="" t="str"><x:v>b</x:v></x:c></x:row>
            </x:sheetData></x:worksheet>
            """;
        var rows = TransportellaReportXlsxSource.ReadRows(new MemoryStream(Encoding.UTF8.GetBytes(sheet)));

        Assert.Equal(["Detail spoje:", "", "100305/4002"], rows[0]);
        Assert.Equal(["", "b"], rows[1]);
    }

    [Fact]
    public void A_report_trip_sheet_gives_one_call_per_stop_with_times_on_the_trip_day()
    {
        // Shape of POVED's StaLineCourse report (5. 9. 2026); dates are month/day/year.
        List<string> Stop(string station, string postCode, string arrival, string departure) =>
            [station, "", postCode, "-", "0", "-", "-", arrival, departure, "", "", "", "", "", "0", "3", "1", "2", "-", "-", "", "", "-", "0 km/h", "- °C", "8P29729", "Autobus"];
        var rows = new List<List<string>>
        {
            new() { "zpět na přehled" },
            new() { "Detail spoje:", "", "100305/4002" },
            new() { "Datum a čas zahájení:", "", "09/05/2026 23:50:35" },
            new() { "Kód zastávky", "Na zavolání", "Název" },
            Stop("14039", "1403901", "23:55:42", "23:56:18"),
            Stop("58003", "5800301", "00:01:22", "00:01:28"),   // after midnight
        };

        var calls = TransportellaReportXlsxSource.ParseTripSheet(rows).ToList();

        Assert.Equal(2, calls.Count);
        Assert.Equal(("100305", "4002", "8P29729", (int?)null, "autobus"), (calls[0].Line, calls[0].TripNumber, calls[0].VehicleCode, calls[0].VehicleId, calls[0].Traction));
        Assert.Equal((14039, (short)1, (int?)1403901), (calls[0].StationId, calls[0].Post, calls[0].StopCode));
        Assert.Equal(new DateTime(2026, 9, 6, 0, 1, 22), calls[1].ActualArrival);
        Assert.NotEqual(calls[0].ExternalId, calls[1].ExternalId);
        Assert.Equal(calls[0].ExternalId, TransportellaReportXlsxSource.ParseTripSheet(rows).First().ExternalId);   // stable
        Assert.Empty(TransportellaReportXlsxSource.ParseTripSheet([new() { "Přehled" }]));
    }

    [Fact]
    public void A_train_sheet_and_a_stop_name_in_the_name_column_are_read()
    {
        var rows = new List<List<string>>
        {
            new() { "Detail vlaku:", "", "7841" },
            new() { "Datum a čas zahájení:", "", "09/05/2026 00:53:00" },
            new() { "Kód zastávky", "Na zavolání", "Název" },
            new() { "5473275", "", "Plzeň hl. n.", "0", "0", "00:53:00", "00:53:00", "00:52:41", "00:53:30" },
        };

        var call = Assert.Single(TransportellaReportXlsxSource.ParseTripSheet(rows));
        Assert.Equal(("7841", 5473275, (short)0, "Plzeň hl. n."), (call.Line, call.StationId, call.Post, call.StopName));
        Assert.Equal(new DateTime(2026, 9, 5, 0, 53, 30), call.ActualDeparture);
    }
}

/// <summary>The import on every engine, and the direct read of Transportella's own SQL Server table.</summary>
public abstract class TransportellaImportContractTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    [Fact]
    public async Task Import_is_idempotent_and_respects_the_period()
    {
        var file = Path.Combine(Path.GetTempPath(), $"stat-{Guid.NewGuid():N}.sql");
        var nextDay = TransportellaStatisticsTests.DumpLine
            .Replace("2397366", "2397367").Replace("2026-01-23", "2026-01-24");
        await File.WriteAllTextAsync(file, TransportellaStatisticsTests.DumpLine + "\n" + nextDay + "\n", TransportellaDumpSource.DefaultEncoding);
        try
        {
            await using var api = new ApiFactory(fixture.Provider, fixture.NewDatabaseConnectionString());
            await using var scope = api.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var importer = new TransportellaStatisticsImporter(db);

            var first = await importer.ImportAsync(new TransportellaDumpSource(file), to: new DateOnly(2026, 1, 23));
            Assert.Equal((2L, 1L, 1L), (first.Read, first.Imported, first.OutsidePeriod));

            var second = await importer.ImportAsync(new TransportellaDumpSource(file));
            Assert.Equal((1L, 1L), (second.Imported, second.AlreadyImported));

            db.ChangeTracker.Clear();
            var calls = await db.RecordedCalls.OrderBy(c => c.ExternalId).ToListAsync();
            Assert.Equal([2397366L, 2397367L], calls.Select(c => c.ExternalId));
            Assert.Equal(RecordedCallSource.TransportellaStatistics, calls[0].Source);
            Assert.Equal(new DateTime(2026, 1, 24, 13, 17, 41), calls[1].ActualDeparture);
        }
        finally
        {
            File.Delete(file);
        }
    }
}

public sealed class PostgresTransportellaImportTests(PostgresFixture fixture) : TransportellaImportContractTests<PostgresFixture>(fixture);

public sealed class SqlServerTransportellaImportTests(SqlServerFixture fixture) : TransportellaImportContractTests<SqlServerFixture>(fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task The_statistics_table_is_read_directly_without_the_driver_column()
    {
        // A stand-in for Transportella's Stat.Statistics with the columns the source reads, plus DriverID.
        var connectionString = _fixture.NewDatabaseConnectionString();
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ToString()))
        {
            await master.OpenAsync();
            await new SqlCommand($"CREATE DATABASE [{database}]", master).ExecuteNonQueryAsync();
        }
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await new SqlCommand("""
                CREATE SCHEMA Stat;
                """, connection).ExecuteNonQueryAsync();
            await new SqlCommand("""
                CREATE TABLE [Stat].[Statistics] (
                  ID int PRIMARY KEY, VehicleLineConTimestamp datetime2 NOT NULL, VehicleID int NOT NULL, DriverID int NOT NULL,
                  Line nvarchar(100) NOT NULL, Connection nvarchar(10) NOT NULL, StationID int NOT NULL, Post smallint NOT NULL,
                  StopName nvarchar(255) NULL, TimeTableArrivalStationDT datetime2 NOT NULL, TimeTableDepartureStationDT datetime2 NOT NULL,
                  ArrivalStationDT datetime2 NOT NULL, DepartureStationDT datetime2 NOT NULL, Traction tinyint NOT NULL);
                INSERT INTO [Stat].[Statistics] VALUES
                  (10, '2026-01-23 13:03', 1665, 4321, '18-1-1|18|1', '15', 104, 0, N'Ladova', '2026-01-23 13:15', '2026-01-23 13:15', '2026-01-23 13:15:29', '2026-01-23 13:17:41', 1),
                  (11, '2026-01-23 13:03', 1665, 4321, '18-1-1|18|1', '15', 105, 2, N'Tomkova', '2026-01-23 13:14', '2026-01-23 13:14', '0001-01-01', '0001-01-01', 1);
                """, connection).ExecuteNonQueryAsync();
        }

        var calls = new List<RecordedCall>();
        await foreach (var call in new TransportellaDatabaseSource(connectionString).ReadAsync(afterExternalId: 10))
        {
            calls.Add(call);
        }

        var only = Assert.Single(calls);   // ID 10 is "already imported"
        Assert.Equal((11L, 105, (short)2, (int?)10502, "Tomkova", "tramvaj"), (only.ExternalId, only.StationId, only.Post, only.StopCode, only.StopName, only.Traction));
        Assert.Null(only.ActualArrival);
        Assert.DoesNotContain("4321", System.Text.Json.JsonSerializer.Serialize(only));
    }
}
