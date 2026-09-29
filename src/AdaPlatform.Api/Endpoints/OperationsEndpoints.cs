using AdaPlatform.Api.Auth;
using AdaPlatform.Infrastructure.Reporting;

namespace AdaPlatform.Api.Endpoints;

/// <summary>Stop operations: how long vehicles stand at stops and what explains it.</summary>
public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        var operations = app.MapGroup("/api/operations").RequireAuthorization(Permissions.OperationsRead);

        // ?line=18 narrows everything to one line; without it, all lines.
        operations.MapGet("/dwell", (StopDwellReport report, int? line, CancellationToken ct) => report.GetAsync(line, ct));
        // One stop's visits (stop detail) and one vehicle's day (trip strip).
        operations.MapGet("/dwell/stops/{code:int}", (StopDwellReport report, int code, int? line, CancellationToken ct) =>
            report.GetStopAsync(code, line, ct));
        operations.MapGet("/vehicles/{vehicle:int}/days/{day}", (StopDwellReport report, int vehicle, DateOnly day, CancellationToken ct) =>
            report.GetVehicleDayAsync(vehicle, day, ct));

        return app;
    }
}
