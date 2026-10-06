using System.Runtime.CompilerServices;
using AdaPlatform.Domain.Operations;
using Microsoft.Data.SqlClient;

namespace AdaPlatform.Infrastructure.Import.Transportella;

/// <summary>
/// Reads Transportella's statistics table directly, for a platform running next to Transportella with
/// read access to its SQL Server database. Asks only for rows newer than the last import and only for
/// the operational columns: the driver column is never selected.
/// </summary>
public sealed class TransportellaDatabaseSource(string connectionString) : ITransportellaStatisticsSource
{
    private const string Query = """
        SELECT [ID], [VehicleLineConTimestamp], [VehicleID], [Line], [Connection], [StationID], [Post], [StopName],
               [TimeTableArrivalStationDT], [TimeTableDepartureStationDT], [ArrivalStationDT], [DepartureStationDT], [Traction]
        FROM [Stat].[Statistics]
        WHERE [ID] > @after
        ORDER BY [ID]
        """;

    public RecordedCallSource Kind => RecordedCallSource.TransportellaStatistics;

    public async IAsyncEnumerable<RecordedCall> ReadAsync(long afterExternalId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(Query, connection) { CommandTimeout = 0 };
        command.Parameters.AddWithValue("@after", afterExternalId);
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, ct);

        while (await reader.ReadAsync(ct))
        {
            DateTime? Time(int i) => reader.IsDBNull(i) ? null : TransportellaValues.DateTime(reader.GetDateTime(i));
            string? Text(int i) => reader.IsDBNull(i) ? null : reader.GetValue(i).ToString();

            // Columns are read in order (SequentialAccess).
            var id = Convert.ToInt64(reader.GetValue(0));
            var tripStart = reader.GetDateTime(1);
            var vehicle = Text(2) ?? "";
            var lineCourse = Text(3);
            var trip = Text(4);
            var station = Convert.ToInt32(reader.GetValue(5));
            var post = reader.IsDBNull(6) ? (short)0 : Convert.ToInt16(reader.GetValue(6));
            var stopName = Text(7);
            var plannedArrival = Time(8);
            var plannedDeparture = Time(9);
            var actualArrival = Time(10);
            var actualDeparture = Time(11);
            var traction = Text(12);

            yield return TransportellaValues.Call(
                RecordedCallSource.TransportellaStatistics, id, vehicle, DateTime.SpecifyKind(tripStart, DateTimeKind.Unspecified),
                lineCourse, TransportellaValues.LineFromCourse(lineCourse), trip, station, post, stopName,
                plannedArrival, plannedDeparture, actualArrival, actualDeparture, traction);
        }
    }
}
