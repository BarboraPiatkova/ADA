using AdaPlatform.Domain.Fleet;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdaPlatform.Infrastructure.Fleet;

/// <summary>
/// Brings the platform's vehicles in line with the operator's register (<see cref="IFleetSource"/>),
/// then fills capacities that no source knows from the per-type table (<see cref="FleetOptions.TypeCapacities"/>).
///
/// Rules:
/// <list type="bullet">
/// <item>The register is authoritative for what it provides: a non-null value overwrites what the
/// logs said. A null never clears anything.</item>
/// <item>Vehicles in the register but not yet in the logs are added, so they are known before their
/// first log arrives. Vehicles missing from the register are left alone, never deleted: their raw
/// data and trips depend on them.</item>
/// <item>A type capacity is used only where the vehicle has no capacity at all, and only for the exact
/// model name configured.</item>
/// </list>
/// Safe to re-run; the same register gives the same result.
/// </summary>
public sealed class FleetSync(AppDbContext db, IFleetSource source, IOptions<FleetOptions> options)
{
    public async Task<FleetSyncReport> SyncAsync(CancellationToken ct = default)
    {
        var report = new FleetSyncReport { Source = source.Name };
        var register = await source.GetVehiclesAsync(ct);
        report.InRegister = register.Count;

        var vehicles = await db.Vehicles.ToDictionaryAsync(v => v.Id, ct);
        foreach (var entry in register)
        {
            var isNew = !vehicles.TryGetValue(entry.Id, out var vehicle);
            if (isNew)
            {
                vehicle = new Vehicle { Id = entry.Id };
                db.Vehicles.Add(vehicle);
                vehicles[entry.Id] = vehicle;
                report.Added++;
            }
            if (Apply(entry, vehicle!) && !isNew)
            {
                report.Updated++;
            }
        }
        report.NotInRegister = register.Count == 0 ? 0 : vehicles.Keys.Except(register.Select(r => r.Id)).Count();

        var types = options.Value.TypeCapacities;
        foreach (var vehicle in vehicles.Values.Where(v => v.SeatingCapacity is null && v.StandingCapacity is null))
        {
            if (vehicle.Model is { } model && types.TryGetValue(model, out var capacity))
            {
                vehicle.SeatingCapacity = capacity.Seating;
                vehicle.StandingCapacity = capacity.Standing;
                report.CapacityFromType++;
            }
            else
            {
                report.WithoutCapacity++;
            }
        }

        await db.SaveChangesAsync(ct);
        return report;
    }

    /// <summary>Copies the register's known values; true when anything changed.</summary>
    private static bool Apply(FleetVehicle entry, Vehicle vehicle)
    {
        var changed = false;
        void Set<T>(T? value, T? current, Action<T> assign)
        {
            if (value is not null && !Equals(value, current))
            {
                assign(value);
                changed = true;
            }
        }

        Set(entry.Model, vehicle.Model, v => vehicle.Model = v);
        Set(entry.Traction, vehicle.Traction, v => vehicle.Traction = v);
        Set(entry.Depot, vehicle.Depot, v => vehicle.Depot = v);
        Set(entry.SeatingCapacity, vehicle.SeatingCapacity, v => vehicle.SeatingCapacity = v);
        Set(entry.StandingCapacity, vehicle.StandingCapacity, v => vehicle.StandingCapacity = v);
        if (entry.IsExcluded is { } excluded && excluded != vehicle.IsExcluded)
        {
            vehicle.IsExcluded = excluded;
            changed = true;
        }
        return changed;
    }
}

/// <summary>For deployments without a register: nothing to pull, type capacities still apply.</summary>
public sealed class NoFleetSource : IFleetSource
{
    public string Name => "none (vehicles from the logs only)";

    public Task<IReadOnlyList<FleetVehicle>> GetVehiclesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FleetVehicle>>([]);
}

public sealed class FleetSyncReport
{
    public string Source { get; set; } = "";
    public int InRegister { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int NotInRegister { get; set; }
    public int CapacityFromType { get; set; }
    public int WithoutCapacity { get; set; }

    public override string ToString() => $"""
        Fleet sync from {Source}: {InRegister} vehicles in the register
          added {Added}, updated {Updated}, in the platform but not in the register {NotInRegister}
          capacity from vehicle type {CapacityFromType}, still without capacity {WithoutCapacity}
        """;
}
