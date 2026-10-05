using AdaPlatform.Api.Auth;
using AdaPlatform.Infrastructure.Reporting;

namespace AdaPlatform.Api.Endpoints;

/// <summary>
/// The fleet register: every vehicle with what its logs hold, and one vehicle's devices, days and faults.
/// Under operations:read, like the vehicle's day it leads to.
/// </summary>
public static class FleetEndpoints
{
    public static IEndpointRouteBuilder MapFleetEndpoints(this IEndpointRouteBuilder app)
    {
        var fleet = app.MapGroup("/api/fleet").RequireAuthorization(Permissions.OperationsRead);

        // ?from=&to=&days= as on the operations screens.
        fleet.MapGet("/vehicles", (FleetReport report, DateOnly? from, DateOnly? to, string? days, CancellationToken ct) =>
            report.GetVehiclesAsync(new ReportPeriod(from, to, ReportPeriod.ParseDays(days)), ct));
        fleet.MapGet("/vehicles/{vehicle:int}", async (FleetReport report, int vehicle, DateOnly? from, DateOnly? to, string? days, CancellationToken ct) =>
            await report.GetVehicleAsync(vehicle, new ReportPeriod(from, to, ReportPeriod.ParseDays(days)), ct) is { } detail
                ? Results.Ok(detail)
                : Results.NotFound());

        return app;
    }
}
