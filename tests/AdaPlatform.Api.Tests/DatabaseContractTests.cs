using System.Net;
using System.Net.Http.Json;
using AdaPlatform.Api.Endpoints;
using AdaPlatform.Api.Tests.Infrastructure;
using AdaPlatform.Domain.Fleet;
using AdaPlatform.Domain.Network;
using AdaPlatform.Domain.Operations;
using AdaPlatform.Domain.Quality;
using AdaPlatform.Domain.Raw;
using AdaPlatform.Infrastructure.Fleet;
using AdaPlatform.Infrastructure.Import.Ada;
using AdaPlatform.Infrastructure.Import.Ucp;
using AdaPlatform.Infrastructure.Persistence;
using AdaPlatform.Infrastructure.Reconstruction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AdaPlatform.Api.Tests;

/// <summary>
/// The behaviour every supported database engine must share. Each engine gets a
/// subclass below, so "switchable database" is a tested property, not a claim.
/// </summary>
public abstract class DatabaseContractTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        await using var api = NewApi();
        using var client = api.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Migrations_are_up_to_date_with_the_model()
    {
        await using var api = NewApi();
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Fails when the model changed but this engine's migration wasn't regenerated.
        Assert.False(db.Database.HasPendingModelChanges(),
            $"The {fixture.Provider} migrations are behind the model — run `dotnet ef migrations add` for it.");
    }

    [Fact]
    public async Task Trip_with_stop_visits_and_door_counts_round_trips_unchanged()
    {
        await using var api = NewApi();
        var start = new DateTime(2024, 10, 1, 6, 35, 0, DateTimeKind.Unspecified);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            SeedNetwork(db);
            var frontDoor = new CountingDevice { VehicleId = 13, DeviceNumber = 41, FirstSeenAt = start, LastSeenAt = start };
            db.CountingDevices.Add(frontDoor);
            db.Trips.Add(new Trip
            {
                StartTime = start,
                EndTime = start.AddMinutes(22),
                PatternCode = 2080001,
                VehicleId = 13,
                Boardings = 38,
                Alightings = 43,
                IsValid = true,
                InitialDelaySeconds = -45,
                Origin = DataOrigin.Measured,
                StopVisits =
                [
                    new StopVisit
                    {
                        Sequence = 1, StopCode = 201, ArrivalTime = start.AddSeconds(52), DelaySeconds = -100,
                        Boardings = 0, Alightings = 1, Occupancy = -1, Origin = DataOrigin.Measured,
                        DoorCounts = [new DoorCount { CountingDevice = frontDoor, Boardings = 0, Alightings = 1, Origin = DataOrigin.Measured }],
                    },
                    new StopVisit
                    {
                        Sequence = 2, StopCode = 301, ArrivalTime = start.AddSeconds(100), DelaySeconds = 33,
                        Boardings = 4, Alightings = 0, Occupancy = 3, Origin = DataOrigin.Imputed,
                    },
                ],
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var trip = await db.Trips.Include(t => t.StopVisits).ThenInclude(s => s.DoorCounts).SingleAsync();

            // Local wall-clock time must survive both engines without a time-zone shift.
            Assert.Equal(start, trip.StartTime);
            Assert.Equal(-45, trip.InitialDelaySeconds);

            var visits = trip.StopVisits.OrderBy(s => s.Sequence).ToList();
            Assert.Equal([-100, 33], visits.Select(s => s.DelaySeconds));
            // Negative occupancy is real counter drift and must be stored as measured.
            Assert.Equal([-1, 3], visits.Select(s => s.Occupancy));
            Assert.Equal([DataOrigin.Measured, DataOrigin.Imputed], visits.Select(s => s.Origin));
            Assert.Equal(1, visits[0].DoorCounts.Single().Alightings);
        }
    }

    [Fact]
    public async Task Stops_endpoint_returns_stops_that_have_coordinates()
    {
        await using var api = NewApi();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            SeedNetwork(db);
            db.Stops.Add(new Stop { Code = 999, Name = "Bez souřadnic" });
            await db.SaveChangesAsync();
        }

        using var client = api.CreateSignedInClient();
        var stops = await client.GetFromJsonAsync<List<NetworkEndpoints.StopDto>>("/api/stops");

        Assert.NotNull(stops);
        Assert.Equal([201, 301], stops.Select(s => s.Code));
        Assert.Equal("Dopravní podnik", stops[0].Name);
    }

    [Fact]
    public async Task Ada_import_translates_legacy_storage_quirks()
    {
        var sqlitePath = await AdaSqliteFixture.CreateAsync();
        try
        {
            await using var api = NewApi();
            await using var scope = api.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var report = await new AdaSqliteImporter(db).ImportAsync(sqlitePath);

            Assert.Equal(1, report.Trips);
            Assert.Equal(1, report.TripsWithNegativeOccupancy);

            db.ChangeTracker.Clear();
            var vehicle = await db.Vehicles.SingleAsync();
            Assert.Null(vehicle.SeatingCapacity);   // ADA's 0 = "not filled in"

            var trip = await db.Trips.Include(t => t.StopVisits).SingleAsync();
            Assert.Equal(-45, trip.InitialDelaySeconds);   // -450,000,000 ticks
            Assert.Equal("208021", trip.BlockCode);
            Assert.Null(trip.SourceFileId);                // legacy: no raw provenance
            // Stop order is derived from time, even though ADA's row ids are reversed.
            Assert.Equal([201, 301], trip.StopVisits.OrderBy(s => s.Sequence).Select(s => s.StopCode));

            var fault = await db.DeviceFaults.SingleAsync();
            Assert.Equal((FaultSource.LegacyAda, trip.Id), (fault.Source, fault.TripId!.Value));
        }
        finally
        {
            File.Delete(sqlitePath);
        }
    }

    [Fact]
    public async Task Ucp_ingestion_stores_every_line_and_is_idempotent()
    {
        var folder = UcpLogFixture.WriteToNewFolder();
        try
        {
            await using var api = NewApi();
            await using var scope = api.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var first = await new UcpLogIngestor(db).IngestAsync(folder);
            var second = await new UcpLogIngestor(db).IngestAsync(folder);

            Assert.Equal((1, 12, 1), (first.FilesIngested, first.Events, first.MalformedLines));
            Assert.Equal((0, 1), (second.FilesIngested, second.FilesSkippedDuplicate));

            db.ChangeTracker.Clear();
            Assert.Equal(12, await db.DeviceEvents.CountAsync());

            var file = await db.SourceFiles.SingleAsync();
            Assert.Equal(new DateOnly(2022, 8, 2), file.ServiceDate);
            Assert.Equal(1, file.MalformedLineCount);

            var vehicle = await db.Vehicles.SingleAsync();
            Assert.Equal((UcpLogFixture.VehicleId, "tramvaj", "Vario LF2R.E"), (vehicle.Id, vehicle.Traction, vehicle.Model));

            var devices = await db.CountingDevices.OrderBy(d => d.DeviceNumber).ToListAsync();
            Assert.Equal([41, 42], devices.Select(d => d.DeviceNumber));
            Assert.Equal("201913121247", devices[0].FirmwareVersion);

            // The typed columns are queryable on both engines — what fault detection relies on.
            Assert.Equal(1, await db.DeviceEvents.CountAsync(e => e.Type == DeviceEventType.Heartbeat && e.Alive == false));
            Assert.Equal(3, await db.DeviceEvents.Where(e => e.Type == DeviceEventType.CountingStopped).SumAsync(e => e.Boardings));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Trip_reconstruction_derives_trips_and_the_network_they_run_on_and_can_be_rerun()
    {
        var folder = UcpLogFixture.WriteToNewFolder();
        try
        {
            await using var api = NewApi();
            await using var scope = api.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await new UcpLogIngestor(db).IngestAsync(folder);
            var reconstruction = new TripReconstruction(db, Options.Create(new ReconstructionOptions()));

            var first = await reconstruction.ReconstructAsync();
            Assert.Equal((1, 0, 1), (first.Files, first.TripsReplaced, first.Stats.Trips));
            Assert.Equal((1, 1, 1), (first.NewLines, first.NewPatterns, first.NewBlocks));

            db.ChangeTracker.Clear();
            var trip = await db.Trips.Include(t => t.StopVisits).ThenInclude(v => v.DoorCounts).ThenInclude(d => d.CountingDevice).SingleAsync();
            Assert.Equal((1200201, "01200715", UcpLogFixture.VehicleId), (trip.PatternCode, trip.BlockCode, trip.VehicleId));
            Assert.True(trip.IsDepotRun);        // from Garaz ED Pisarky
            Assert.False(trip.IsValid);          // the stop summary flags device 42
            Assert.NotNull(trip.SourceFileId);

            var visits = trip.StopVisits.OrderBy(v => v.Sequence).ToList();
            Assert.Equal([165902, 134302], visits.Select(v => v.StopCode));
            Assert.Equal((3, 1, 2, false), (visits[0].Boardings, visits[0].Alightings, visits[0].Occupancy, visits[0].IsPassThrough));
            Assert.True(visits[1].IsPassThrough);
            var doors = visits[0].DoorCounts.ToDictionary(d => d.CountingDevice.DeviceNumber);
            Assert.Equal((2, 0, false), (doors[41].Boardings, doors[41].Alightings, doors[41].IsFlaggedInvalid));
            Assert.Equal((1, 1, true), (doors[42].Boardings, doors[42].Alightings, doors[42].IsFlaggedInvalid));

            var stops = await db.Stops.ToDictionaryAsync(s => s.Code);
            Assert.Equal("Technologicky park", stops[165902].Name);
            Assert.Equal(49.231468, stops[165902].Latitude);
            Assert.Equal(("Lipova", "Garaz ED Pisarky", "Komarov"), (stops[134302].Name, stops[941002].Name, stops[125601].Name));
            Assert.Equal(12, (await db.Patterns.SingleAsync()).LineId);

            // Re-running replaces the trips instead of adding more; an import-time run finds nothing new.
            var again = await reconstruction.ReconstructAsync();
            Assert.Equal((1, 1, 0), (again.Files, again.TripsReplaced, again.NewStops));
            Assert.Equal(0, (await reconstruction.ReconstructAsync(onlyNew: true)).Files);
            db.ChangeTracker.Clear();
            Assert.Equal((1, 2, 2), (await db.Trips.CountAsync(), await db.StopVisits.CountAsync(), await db.DoorCounts.CountAsync()));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Fleet_sync_merges_the_register_and_fills_capacity_from_the_vehicle_type()
    {
        await using var api = NewApi();
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Known from the logs: traction and depot, no type, no capacity.
        db.Vehicles.Add(new Vehicle { Id = 58, Traction = "trolejbus", Depot = "1" });
        await db.SaveChangesAsync();

        var register = new StaticFleetSource(
            new FleetVehicle(58, Model: "SOR 30 TR"),
            new FleetVehicle(38, "SOR NB 12", "autobus", SeatingCapacity: 26, StandingCapacity: 76),
            new FleetVehicle(99, "Trenažér", IsExcluded: true));
        var options = Options.Create(new FleetOptions { TypeCapacities = { ["SOR 30 TR"] = new TypeCapacity(32, 62) } });

        var first = await new FleetSync(db, register, options).SyncAsync();
        Assert.Equal((3, 2, 1, 1, 1), (first.InRegister, first.Added, first.Updated, first.CapacityFromType, first.WithoutCapacity));

        db.ChangeTracker.Clear();
        var vehicles = await db.Vehicles.ToDictionaryAsync(v => v.Id);
        Assert.Equal(("SOR 30 TR", "trolejbus", "1", 32, 62), (vehicles[58].Model, vehicles[58].Traction, vehicles[58].Depot, vehicles[58].SeatingCapacity, vehicles[58].StandingCapacity));
        Assert.Equal((26, 76), (vehicles[38].SeatingCapacity, vehicles[38].StandingCapacity));   // the register's own figures win
        Assert.True(vehicles[99].IsExcluded);

        // The same register again changes nothing.
        var again = await new FleetSync(db, register, options).SyncAsync();
        Assert.Equal((0, 0, 0), (again.Added, again.Updated, again.CapacityFromType));
    }

    [Fact]
    public async Task Dwell_report_relates_stop_time_to_passengers_and_lists_what_they_dont_explain()
    {
        await using var api = NewApi();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            SeedNetwork(db);
            var file = new SourceFile { SourcePath = "t", FileName = "APC_13.2022-08-01.csv", Sha256 = "x", VehicleId = 13 };
            db.SourceFiles.Add(file);
            var t0 = new DateTime(2022, 8, 1, 7, 0, 0);
            StopVisit Visit(int sequence, int stop, int minute, int dwell, int passengers, int delay = 0) => new()
            {
                Sequence = sequence,
                StopCode = stop,
                ArrivalTime = t0.AddMinutes(minute),
                DepartureTime = t0.AddMinutes(minute).AddSeconds(dwell),
                Boardings = passengers,
                Alightings = 0,
                DelaySeconds = delay,
            };
            // Middle stops follow dwell = 10 s + 1 s per passenger exactly; one stop stands 150 s with 2
            // passengers and leaves on time. First and last stops (the layover) must not count.
            for (var trip = 0; trip < 3; trip++)
            {
                db.Trips.Add(new Trip
                {
                    VehicleId = 13,
                    PatternCode = 2080001,
                    SourceFile = file,
                    IsValid = true,
                    StartTime = t0,
                    EndTime = t0.AddHours(1),
                    StopVisits =
                    [
                        Visit(1, 201, 0, 900, 0),
                        Visit(2, 301, 10, 10, 0),
                        Visit(3, 301, 20, 20, 10),
                        Visit(4, 301, 30, 30, 20),
                        Visit(5, 201, 40, trip == 0 ? 150 : 12, 2, delay: 20),
                        Visit(6, 201, 50, 600, 0),
                    ],
                });
            }
            // An invalid trip is left out entirely.
            db.Trips.Add(new Trip { VehicleId = 13, SourceFile = file, IsValid = false, StartTime = t0, EndTime = t0, StopVisits = [Visit(1, 201, 0, 1, 0), Visit(2, 301, 1, 999, 0), Visit(3, 201, 2, 1, 0)] });
            await db.SaveChangesAsync();
        }

        using var report = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/operations/dwell"));
        var root = report.RootElement;
        var model = root.GetProperty("model");

        Assert.Equal(12, model.GetProperty("visits").GetInt32());   // 4 middle stops × 3 trips
        Assert.Equal([208], root.GetProperty("lines").EnumerateArray().Select(l => l.GetInt32()));
        var unexplained = Assert.Single(root.GetProperty("unexplained").EnumerateArray());
        Assert.Equal((150, 2, "HeldForTimetable"), (unexplained.GetProperty("dwellSeconds").GetInt32(), unexplained.GetProperty("passengers").GetInt32(), unexplained.GetProperty("cause").GetString()));

        var bands = root.GetProperty("bands").EnumerateArray().ToDictionary(b => b.GetProperty("minPassengers").GetInt32());
        Assert.Equal((3, 10.0), (bands[0].GetProperty("visits").GetInt32(), bands[0].GetProperty("medianSeconds").GetDouble()));
        Assert.Equal(20.0, bands[6].GetProperty("medianSeconds").GetDouble());
        Assert.Equal(30.0, bands[11].GetProperty("medianSeconds").GetDouble());

        // Every day with data is listed for the period picker, and a period without trips is empty.
        Assert.Equal(["2022-08-01"], root.GetProperty("days").EnumerateArray().Select(d => d.GetString()));
        using var inPeriod = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/operations/dwell?from=2022-08-01&to=2022-08-01"));
        Assert.Equal(12, inPeriod.RootElement.GetProperty("model").GetProperty("visits").GetInt32());
        using var outside = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/operations/dwell?from=2022-08-02"));
        Assert.Equal(0, outside.RootElement.GetProperty("model").GetProperty("visits").GetInt32());
        Assert.Equal(["2022-08-01"], outside.RootElement.GetProperty("days").EnumerateArray().Select(d => d.GetString()));
        // 1 August 2022 was a Monday: kept on working days, left out on Sundays.
        using var workdays = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/operations/dwell?days=workdays"));
        Assert.Equal(12, workdays.RootElement.GetProperty("model").GetProperty("visits").GetInt32());
        using var sundays = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/operations/dwell?days=sundayOrHoliday"));
        Assert.Equal(0, sundays.RootElement.GetProperty("model").GetProperty("visits").GetInt32());

        // A line with no data gives an empty report, not an error.
        using var other = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/operations/dwell?line=999"));
        Assert.Equal(0, other.RootElement.GetProperty("model").GetProperty("visits").GetInt32());

        // Stop detail: every counted visit of the stop, the long one marked.
        using var stop = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/operations/dwell/stops/201"));
        var stopVisits = stop.RootElement.GetProperty("visits").EnumerateArray().ToList();
        Assert.Equal(3, stopVisits.Count);   // sequence 5 of each trip; first and last stops excluded
        Assert.Equal(1, stopVisits.Count(v => v.GetProperty("unexplained").GetBoolean()));

        // Vehicle day: all trips with every stop, invalid ones included and marked.
        using var day = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/operations/vehicles/13/days/2022-08-01"));
        var trips = day.RootElement.GetProperty("trips").EnumerateArray().ToList();
        Assert.Equal(4, trips.Count);
        Assert.Equal(1, trips.Count(x => !x.GetProperty("isValid").GetBoolean()));
        Assert.Equal(6, trips.First(x => x.GetProperty("isValid").GetBoolean()).GetProperty("stops").GetArrayLength());
        Assert.Equal(["2022-08-01"], day.RootElement.GetProperty("days").EnumerateArray().Select(d => d.GetString()));
    }

    [Fact]
    public async Task Punctuality_and_load_reports_judge_departures_and_follow_the_load_along_a_trip()
    {
        await using var api = NewApi();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            SeedNetwork(db);
            db.Vehicles.Local.Single(v => v.Id == 13).SeatingCapacity = 20;
            db.Vehicles.Local.Single(v => v.Id == 13).StandingCapacity = 20;
            var file = new SourceFile { SourcePath = "t", FileName = "APC_13.2022-08-01.csv", Sha256 = "x", VehicleId = 13 };
            db.SourceFiles.Add(file);
            var t0 = new DateTime(2022, 8, 1, 7, 0, 0);
            StopVisit Visit(int sequence, int stop, int delay, int on, int off) => new()
            {
                Sequence = sequence,
                StopCode = stop,
                ArrivalTime = t0.AddMinutes(sequence),
                DepartureTime = t0.AddMinutes(sequence).AddSeconds(20),
                DelaySeconds = delay,
                Boardings = on,
                Alightings = off,
            };
            // Loads after each stop: 10, 30, 25, 0. Delays: early, on time, late, very late.
            db.Trips.Add(new Trip
            {
                VehicleId = 13,
                PatternCode = 2080001,
                SourceFile = file,
                IsValid = true,
                StartTime = t0,
                EndTime = t0.AddHours(1),
                StopVisits = [Visit(1, 201, -120, 10, 0), Visit(2, 301, 0, 20, 0), Visit(3, 201, 240, 5, 10), Visit(4, 301, 600, 0, 25)],
            });
            await db.SaveChangesAsync();
        }
        var client = api.CreateSignedInClient();

        using var punctuality = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/api/operations/punctuality"));
        var total = punctuality.RootElement.GetProperty("total");
        Assert.Equal((4, 1, 1, 1, 1),
            (total.GetProperty("departures").GetInt32(), total.GetProperty("early").GetInt32(), total.GetProperty("onTime").GetInt32(),
             total.GetProperty("late").GetInt32(), total.GetProperty("veryLate").GetInt32()));
        // Beyond the 3-minute limit: 25 passengers × 1 min at the third stop, nobody on board at the fourth.
        Assert.Equal(25, total.GetProperty("passengerMinutesLate").GetDouble());
        Assert.Equal(30.0 / 65, total.GetProperty("passengersOnTimeShare").GetDouble(), 3);
        Assert.Equal([7], punctuality.RootElement.GetProperty("hours").EnumerateArray().Select(h => h.GetProperty("hour").GetInt32()));
        var monday = Assert.Single(punctuality.RootElement.GetProperty("weekdays").EnumerateArray());
        Assert.Equal((1, 1, 4), (monday.GetProperty("weekday").GetInt32(), monday.GetProperty("days").GetInt32(), monday.GetProperty("summary").GetProperty("departures").GetInt32()));
        var cell = Assert.Single(punctuality.RootElement.GetProperty("weekHours").EnumerateArray());
        Assert.Equal((1, 7), (cell.GetProperty("weekday").GetInt32(), cell.GetProperty("hour").GetInt32()));
        using var earlier = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/api/operations/punctuality?to=2022-07-31"));
        Assert.Equal(0, earlier.RootElement.GetProperty("total").GetProperty("departures").GetInt32());
        using var loadWeek = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/api/operations/load"));
        var loadMonday = Assert.Single(loadWeek.RootElement.GetProperty("boardingsByWeekday").EnumerateArray());
        Assert.Equal((1, 1, 35), (loadMonday.GetProperty("weekday").GetInt32(), loadMonday.GetProperty("days").GetInt32(), loadMonday.GetProperty("boardings").GetInt32()));
        using var loadSaturdays = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/api/operations/load?days=saturday"));
        Assert.Equal(0, loadSaturdays.RootElement.GetProperty("trips").GetInt32());
        using var loadLater = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/api/operations/load?from=2022-08-02"));
        Assert.Equal(0, loadLater.RootElement.GetProperty("trips").GetInt32());

        using var load = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/api/operations/load"));
        var profile = load.RootElement.GetProperty("profile").EnumerateArray().ToList();
        Assert.Equal([10.0, 30.0, 25.0, 0.0], profile.Select(p => p.GetProperty("medianLoad").GetDouble()));
        var crowded = Assert.Single(load.RootElement.GetProperty("crowded").EnumerateArray());
        Assert.Equal((30, 301, 0.75), (crowded.GetProperty("peakLoad").GetInt32(), crowded.GetProperty("peakStopCode").GetInt32(), crowded.GetProperty("peakShare").GetDouble()));
        Assert.Equal(35, load.RootElement.GetProperty("boardings").GetInt32());
    }

    [Fact]
    public async Task Dwell_report_needs_the_operations_permission()
    {
        await using var api = NewApi();
        var response = await api.CreateSignedInClient(AdaPlatform.Api.Auth.Permissions.QualityRead).GetAsync("/api/operations/dwell");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class StaticFleetSource(params FleetVehicle[] vehicles) : IFleetSource
    {
        public string Name => "test register";

        public Task<IReadOnlyList<FleetVehicle>> GetVehiclesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FleetVehicle>>(vehicles);
    }

    [Fact]
    public async Task Device_health_report_explains_status_with_reason_codes()
    {
        var folder = UcpLogFixture.WriteToNewFolder();
        try
        {
            await using var api = NewApi();
            await using (var scope = api.Services.CreateAsyncScope())
            {
                await new UcpLogIngestor(scope.ServiceProvider.GetRequiredService<AppDbContext>()).IngestAsync(folder);
            }

            using var report = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/quality/devices"));
            var vehicle = report.RootElement.GetProperty("vehicles").EnumerateArray().Single();
            string[] Codes(System.Text.Json.JsonElement e) =>
                e.GetProperty("reasons").EnumerateArray().Select(r => r.GetProperty("code").GetString()!).ToArray();

            // The fixture's only stop summary flags device 42 (chyba=[42]); 3 in / 1 out is
            // too few passengers to judge the balance.
            Assert.Equal("Warning", vehicle.GetProperty("status").GetString());
            Assert.Equal(["FlaggedStops"], Codes(vehicle));
            Assert.Equal(System.Text.Json.JsonValueKind.Null, vehicle.GetProperty("imbalance").ValueKind);

            // Per door: counts are stop − start readings (41: 2 in; 42: 1 in, 1 out).
            var devices = vehicle.GetProperty("devices").EnumerateArray().ToDictionary(d => d.GetProperty("deviceNumber").GetInt32());
            Assert.Equal((2, 0), (devices[41].GetProperty("boardings").GetInt32(), devices[41].GetProperty("alightings").GetInt32()));
            Assert.Equal(["DeviceFlagged", "DeviceNotAlive"], Codes(devices[42]));

            // A period without the fixture's day (Tuesday 2 August 2022, in the summer holidays) has no
            // vehicles, and still lists the day for the period picker.
            using var later = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/quality/devices?from=2022-08-03"));
            Assert.Empty(later.RootElement.GetProperty("vehicles").EnumerateArray());
            Assert.Equal(["2022-08-02"], later.RootElement.GetProperty("days").EnumerateArray().Select(d => d.GetString()));
            using var holidays = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/quality/devices?days=holidayWorkdays"));
            Assert.Single(holidays.RootElement.GetProperty("vehicles").EnumerateArray());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Daily_quality_report_has_one_row_per_vehicle_and_day()
    {
        var folder = UcpLogFixture.WriteToNewFolder();
        try
        {
            await using var api = NewApi();
            await using (var scope = api.Services.CreateAsyncScope())
            {
                await new UcpLogIngestor(scope.ServiceProvider.GetRequiredService<AppDbContext>()).IngestAsync(folder);
            }

            using var report = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/quality/daily"));
            var day = report.RootElement.EnumerateArray().Single();

            Assert.Equal(UcpLogFixture.VehicleId, day.GetProperty("vehicleId").GetInt32());
            Assert.Equal("2022-08-02", day.GetProperty("day").GetString());
            Assert.Equal((3, 1), (day.GetProperty("boardings").GetInt32(), day.GetProperty("alightings").GetInt32()));
            Assert.Equal(1.0, day.GetProperty("flaggedStopShare").GetDouble());    // the one stop flags device 42
            Assert.Equal(0.0, day.GetProperty("negativeOccupancyShare").GetDouble());

            using var sundays = System.Text.Json.JsonDocument.Parse(await api.CreateSignedInClient().GetStringAsync("/api/quality/daily?days=sundayOrHoliday"));
            Assert.Empty(sundays.RootElement.EnumerateArray());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private ApiFactory NewApi() => new(fixture.Provider, fixture.NewDatabaseConnectionString());

    private static void SeedNetwork(AppDbContext db)
    {
        db.Stops.AddRange(
            new Stop { Code = 201, Name = "Dopravní podnik", Latitude = 49.3865, Longitude = 15.6002 },
            new Stop { Code = 301, Name = "Brtnická ul.", Latitude = 49.3871, Longitude = 15.6010 });
        db.Lines.Add(new Line { Id = 208 });
        db.Patterns.Add(new Pattern { Code = 2080001, LineId = 208 });
        db.Vehicles.Add(new Vehicle { Id = 13 });
    }
}

public sealed class PostgresContractTests(PostgresFixture fixture) : DatabaseContractTests<PostgresFixture>(fixture);

public sealed class SqlServerContractTests(SqlServerFixture fixture) : DatabaseContractTests<SqlServerFixture>(fixture);
