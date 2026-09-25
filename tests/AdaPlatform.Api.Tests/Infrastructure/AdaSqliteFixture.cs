using Microsoft.Data.Sqlite;

namespace AdaPlatform.Api.Tests.Infrastructure;

/// <summary>
/// A tiny ADA.dbFile: the real ADA schema (copied from a production file) with one ride
/// that exhibits each quirk the importer must translate.
/// </summary>
public static class AdaSqliteFixture
{
    public static async Task<string> CreateAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ada-fixture-{Guid.NewGuid():N}.dbFile");
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE ApcErrors (Id INTEGER not null, DeviceID INTEGER, TimeFrom TEXT, TimeTo TEXT, VehicleID INTEGER, ErrorId INTEGER, Ride_id INTEGER, primary key (Id));
            CREATE TABLE Lines (Id INTEGER not null, primary key (Id));
            CREATE TABLE Rides (Id INTEGER not null, APCCount INTEGER, StartDateTime TEXT, GetIn INTEGER, GetOut INTEGER, isValid INTEGER, InitialDelay INTEGER, CreationType TEXT, StopTime TEXT, Variation INTEGER, vehicle_id INTEGER, secondVehicle_id INTEGER, service_id INTEGER, trace_id INTEGER, primary key (Id));
            CREATE TABLE Services (Code INTEGER not null, primary key (Code));
            CREATE TABLE Stations (CisCode INTEGER not null, Name TEXT, Tariffs TEXT, Altitude REAL, Latitude REAL, Longitude REAL, JTSKX REAL, JTSKY REAL, primary key (CisCode));
            CREATE TABLE StationsOnTrace (Id INTEGER not null, userOrder INTEGER, station_id INTEGER, trace_id INTEGER, primary key (Id));
            CREATE TABLE Traces (code INTEGER not null, TargetCode INTEGER, FromStation TEXT, ToStation TEXT, Length REAL, line_id INTEGER, primary key (code));
            CREATE TABLE Vehicles (Id INTEGER not null, ApcCount INTEGER, B2hostID TEXT, CapacityForSeating INTEGER, CapacityForStanding INTEGER, TractionType TEXT, Type TEXT, Blacklisted INTEGER, LastImportDate TEXT, primary key (Id));
            CREATE TABLE VehicleStopRecords (Id INTEGER not null, Delay INTEGER, Time TEXT, PeopleCount INTEGER, PeopleIn INTEGER, PeopleOut INTEGER, InvalidApc TEXT, ride_id INTEGER, station_id INTEGER, primary key (Id));

            INSERT INTO Stations VALUES (201, 'Dopravní podnik', NULL, 200.0, 49.386467, 15.60024, -668556.28, -1131247.41);
            INSERT INTO Stations VALUES (301, 'Brtnická ul.', NULL, 200.0, 49.3871, 15.6010, -668500.0, -1131200.0);
            INSERT INTO Lines VALUES (208);
            INSERT INTO Traces VALUES (2080001, 0, '201 Dopravní podnik', '301 Brtnická ul.', NULL, 208);
            INSERT INTO StationsOnTrace VALUES (1, 1, 201, 2080001), (2, 2, 301, 2080001);
            INSERT INTO Services VALUES (208021);
            INSERT INTO Vehicles VALUES (13, 3, NULL, 0, 0, 'NA', NULL, 0, '2024-10-31 22:36:00');
            INSERT INTO Rides VALUES (1, 3, '2024-10-01 06:35:00', 38, 43, 0, -450000000, 'FromApc', '2024-10-01 06:57:00', 1, 13, NULL, 208021, 2080001);
            -- Row ids deliberately in reverse stop order: the importer must order by time.
            INSERT INTO VehicleStopRecords VALUES (10, -330000000, '2024-10-01 06:36:40', 3, 4, 0, '', 1, 301);
            INSERT INTO VehicleStopRecords VALUES (11, -1000000000, '2024-10-01 06:35:52', -1, 0, 1, '', 1, 201);
            INSERT INTO ApcErrors VALUES (1, 0, '2024-10-01 06:57:11', NULL, 13, 0, 1);
            """;
        await command.ExecuteNonQueryAsync();
        return path;
    }
}
