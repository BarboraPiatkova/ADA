using System.Net;
using System.Net.Http.Json;
using AdaPlatform.Api.Endpoints;
using AdaPlatform.Api.Tests.Infrastructure;
using AdaPlatform.Domain.Fleet;
using AdaPlatform.Domain.Network;
using AdaPlatform.Domain.Operations;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        using var client = api.CreateClient();
        var stops = await client.GetFromJsonAsync<List<NetworkEndpoints.StopDto>>("/api/stops");

        Assert.NotNull(stops);
        Assert.Equal([201, 301], stops.Select(s => s.Code));
        Assert.Equal("Dopravní podnik", stops[0].Name);
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
