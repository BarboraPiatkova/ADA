using System.Globalization;
using AdaPlatform.Domain.Fleet;
using AdaPlatform.Domain.Network;
using AdaPlatform.Domain.Operations;
using AdaPlatform.Domain.Quality;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Import.Ada;

/// <summary>
/// Copies a legacy ADA SQLite database into the platform schema. ADA only stored derived
/// data, so this fills the reference and derived layers — there are no raw events and no
/// per-door counts to import. ADA's storage quirks are translated on the way: delays in
/// .NET ticks → seconds, capacity 0 → unknown (null), stop order derived from time.
/// </summary>
public sealed class AdaSqliteImporter(AppDbContext db)
{
    public async Task<AdaImportReport> ImportAsync(string sqlitePath, CancellationToken ct = default)
    {
        if (await db.Stops.AnyAsync(ct) || await db.Trips.AnyAsync(ct))
        {
            throw new InvalidOperationException(
                "Target database already contains reference or trip data. The ADA import is a " +
                "one-off seed — run it against an empty database.");
        }

        // No pooling: a pooled connection keeps the file locked after the import finishes.
        await using var sqlite = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = sqlitePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await sqlite.OpenAsync(ct);

        var stops = await ReadAsync(sqlite, "SELECT CisCode, Name, Tariffs, Altitude, Latitude, Longitude, JTSKX, JTSKY FROM Stations",
            r => new Stop
            {
                Code = r.GetInt32(0),
                Name = r.IsDBNull(1) ? "" : r.GetString(1),
                Tariffs = NullIfEmpty(r, 2),
                Altitude = NullableDouble(r, 3),
                Latitude = NullableDouble(r, 4),
                Longitude = NullableDouble(r, 5),
                JtskX = NullableDouble(r, 6),
                JtskY = NullableDouble(r, 7),
            }, ct);

        var lines = await ReadAsync(sqlite, "SELECT Id FROM Lines", r => new Line { Id = r.GetInt32(0) }, ct);

        var patterns = await ReadAsync(sqlite, "SELECT code, TargetCode, FromStation, ToStation, Length, line_id FROM Traces",
            r => new Pattern
            {
                Code = r.GetInt32(0),
                TargetCode = r.IsDBNull(1) ? 0 : r.GetInt32(1),
                FirstStopName = NullIfEmpty(r, 2),
                LastStopName = NullIfEmpty(r, 3),
                LengthKm = NullableDouble(r, 4),
                LineId = r.GetInt32(5),
            }, ct);

        var patternStops = await ReadAsync(sqlite, "SELECT trace_id, userOrder, station_id FROM StationsOnTrace",
            r => new PatternStop { PatternCode = r.GetInt32(0), Sequence = r.GetInt32(1), StopCode = r.GetInt32(2) }, ct);

        var blocks = await ReadAsync(sqlite, "SELECT Code FROM Services",
            r => new Block { Code = r.GetInt64(0).ToString(CultureInfo.InvariantCulture) }, ct);

        var vehicles = await ReadAsync(sqlite,
            "SELECT Id, CapacityForSeating, CapacityForStanding, TractionType, Type, Blacklisted FROM Vehicles",
            r => new Vehicle
            {
                Id = r.GetInt32(0),
                SeatingCapacity = PositiveOrNull(r, 1),
                StandingCapacity = PositiveOrNull(r, 2),
                Traction = NullIfEmpty(r, 3),
                Model = NullIfEmpty(r, 4),
                IsExcluded = !r.IsDBNull(5) && r.GetInt32(5) != 0,
            }, ct);

        var tripsByAdaId = new Dictionary<long, Trip>();
        foreach (var (adaId, trip) in await ReadAsync(sqlite,
            "SELECT Id, StartDateTime, GetIn, GetOut, isValid, InitialDelay, CreationType, StopTime, vehicle_id, secondVehicle_id, service_id, trace_id FROM Rides",
            r => (r.GetInt64(0), new Trip
            {
                StartTime = ParseTime(r.GetString(1)),
                Boardings = NullableInt(r, 2) ?? 0,
                Alightings = NullableInt(r, 3) ?? 0,
                IsValid = !r.IsDBNull(4) && r.GetInt32(4) != 0,
                InitialDelaySeconds = TicksToSeconds(r, 5),
                Origin = ParseOrigin(r.GetString(6)),
                EndTime = ParseTime(r.GetString(7)),
                VehicleId = r.GetInt32(8),
                SecondVehicleId = NullableInt(r, 9),
                BlockCode = r.IsDBNull(10) ? null : r.GetInt64(10).ToString(CultureInfo.InvariantCulture),
                PatternCode = NullableInt(r, 11),
            }), ct))
        {
            tripsByAdaId[adaId] = trip;
        }

        var stopRecords = await ReadAsync(sqlite,
            "SELECT ride_id, Id, Time, Delay, PeopleIn, PeopleOut, PeopleCount, station_id FROM VehicleStopRecords",
            r => (AdaTripId: r.GetInt64(0), AdaId: r.GetInt64(1), Visit: new StopVisit
            {
                ArrivalTime = ParseTime(r.GetString(2)),
                DelaySeconds = TicksToSeconds(r, 3),
                Boardings = NullableInt(r, 4) ?? 0,
                Alightings = NullableInt(r, 5) ?? 0,
                Occupancy = NullableInt(r, 6) ?? 0,
                StopCode = r.GetInt32(7),
                Origin = DataOrigin.Measured,
            }), ct);

        // ADA has no stop order column; order by time within each trip (ADA id breaks ties).
        foreach (var group in stopRecords.GroupBy(s => s.AdaTripId))
        {
            var trip = tripsByAdaId[group.Key];
            var sequence = 1;
            foreach (var (_, _, visit) in group.OrderBy(s => s.Visit.ArrivalTime).ThenBy(s => s.AdaId))
            {
                visit.Sequence = sequence++;
                visit.Origin = trip.Origin;
                trip.StopVisits.Add(visit);
            }
        }

        var faults = await ReadAsync(sqlite, "SELECT DeviceID, VehicleID, ErrorId, TimeFrom, TimeTo, Ride_id FROM ApcErrors",
            r => new DeviceFault
            {
                DeviceNumber = PositiveOrNull(r, 0),
                VehicleId = NullableInt(r, 1) ?? 0,
                Kind = (FaultKind)(NullableInt(r, 2) ?? 0),
                From = ParseTime(r.GetString(3)),
                To = r.IsDBNull(4) ? null : ParseTime(r.GetString(4)),
                Trip = r.IsDBNull(5) ? null : tripsByAdaId.GetValueOrDefault(r.GetInt64(5)),
                Source = FaultSource.LegacyAda,
            }, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Reference data first, in its own save, so the (large) trip save only has to
        // resolve foreign keys to rows that already exist.
        db.Stops.AddRange(stops);
        db.Lines.AddRange(lines);
        db.Patterns.AddRange(patterns);
        db.PatternStops.AddRange(patternStops);
        db.Blocks.AddRange(blocks);
        db.Vehicles.AddRange(vehicles);
        await db.SaveChangesAsync(ct);

        db.Trips.AddRange(tripsByAdaId.Values);
        db.DeviceFaults.AddRange(faults);
        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        var trips = tripsByAdaId.Values;
        return new AdaImportReport
        {
            Stops = stops.Count,
            Lines = lines.Count,
            Patterns = patterns.Count,
            PatternStops = patternStops.Count,
            Blocks = blocks.Count,
            Vehicles = vehicles.Count,
            Trips = trips.Count,
            StopVisits = stopRecords.Count,
            DeviceFaults = faults.Count,
            From = trips.Count > 0 ? trips.Min(t => t.StartTime) : null,
            To = trips.Count > 0 ? trips.Max(t => t.StartTime) : null,
            VehiclesWithUnknownCapacity = vehicles.Count(v => v.TotalCapacity is null),
            TripsWithNegativeOccupancy = trips.Count(t => t.StopVisits.Any(s => s.Occupancy < 0)),
        };
    }

    private static async Task<List<T>> ReadAsync<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> map, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<T>();
        while (await reader.ReadAsync(ct))
        {
            result.Add(map(reader));
        }
        return result;
    }

    // ADA (NHibernate) writes "yyyy-MM-dd HH:mm:ss" local times.
    private static DateTime ParseTime(string value) =>
        DateTime.SpecifyKind(DateTime.Parse(value, CultureInfo.InvariantCulture), DateTimeKind.Unspecified);

    // ADA stores TimeSpan delays as .NET ticks (100 ns).
    private static int TicksToSeconds(SqliteDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? 0 : (int)Math.Round((double)r.GetInt64(ordinal) / TimeSpan.TicksPerSecond);

    private static int? NullableInt(SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetInt32(ordinal);

    private static double? NullableDouble(SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetDouble(ordinal);

    private static int? PositiveOrNull(SqliteDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) || r.GetInt32(ordinal) <= 0 ? null : r.GetInt32(ordinal);

    private static string? NullIfEmpty(SqliteDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) || r.GetString(ordinal).Length == 0 ? null : r.GetString(ordinal);

    private static DataOrigin ParseOrigin(string adaCreationType) => adaCreationType switch
    {
        "FromApc" or "FromEyeOne" => DataOrigin.Measured,
        "Written" => DataOrigin.Manual,
        "Extrapolated" => DataOrigin.Imputed,
        _ => throw new InvalidOperationException($"Unknown ADA CreationType '{adaCreationType}'."),
    };
}

public sealed class AdaImportReport
{
    public int Stops { get; init; }
    public int Lines { get; init; }
    public int Patterns { get; init; }
    public int PatternStops { get; init; }
    public int Blocks { get; init; }
    public int Vehicles { get; init; }
    public int Trips { get; init; }
    public int StopVisits { get; init; }
    public int DeviceFaults { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int VehiclesWithUnknownCapacity { get; init; }
    public int TripsWithNegativeOccupancy { get; init; }

    public override string ToString() => $"""
        Imported legacy ADA data ({From:yyyy-MM-dd} → {To:yyyy-MM-dd}):
          stops {Stops}, lines {Lines}, patterns {Patterns}, pattern stops {PatternStops}, blocks {Blocks}
          vehicles {Vehicles}, trips {Trips}, stop visits {StopVisits}, device faults {DeviceFaults}
        Data-quality notes:
          vehicles with unknown capacity: {VehiclesWithUnknownCapacity} of {Vehicles} (occupancy % not computable for them)
          trips whose occupancy goes negative at some stop: {TripsWithNegativeOccupancy} of {Trips} (counter drift)
        """;
}
