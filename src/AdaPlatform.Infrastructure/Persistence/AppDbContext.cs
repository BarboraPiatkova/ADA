using AdaPlatform.Domain.Fleet;
using AdaPlatform.Domain.Network;
using AdaPlatform.Domain.Operations;
using AdaPlatform.Domain.Quality;
using AdaPlatform.Domain.Raw;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Persistence;

/// <summary>
/// Single EF Core context for the platform. The model is provider-neutral; the only
/// provider-specific line is the DateTime column type in <see cref="ConfigureConventions"/>.
/// One deployment serves one operator (ADR 0002), so there is no tenant filtering here.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Reference: the network as the timetable defines it.
    public DbSet<Stop> Stops => Set<Stop>();
    public DbSet<Line> Lines => Set<Line>();
    public DbSet<Pattern> Patterns => Set<Pattern>();
    public DbSet<PatternStop> PatternStops => Set<PatternStop>();
    public DbSet<Block> Blocks => Set<Block>();

    // Fleet: vehicles and their per-door counting devices.
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<CountingDevice> CountingDevices => Set<CountingDevice>();

    // Raw: what the devices reported, never modified.
    public DbSet<SourceFile> SourceFiles => Set<SourceFile>();
    public DbSet<DeviceEvent> DeviceEvents => Set<DeviceEvent>();

    // Derived: trips reconstructed from raw events (or imported from legacy ADA).
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<StopVisit> StopVisits => Set<StopVisit>();
    public DbSet<DoorCount> DoorCounts => Set<DoorCount>();

    // Operations as the dispatch system recorded them (Transportella), joined to the counts per stop.
    public DbSet<RecordedCall> RecordedCalls => Set<RecordedCall>();

    // Quality.
    public DbSet<DeviceFault> DeviceFaults => Set<DeviceFault>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Transit data is local wall-clock time (timetables are published that way), so
        // store it without a time zone on both engines. SQL Server's datetime2 already
        // behaves like that; Npgsql defaults DateTime to timestamptz, which rejects
        // non-UTC values, so Postgres needs the explicit type.
        if (Database.IsNpgsql())
        {
            configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
