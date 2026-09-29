using AdaPlatform.Api.Auth;
using AdaPlatform.Infrastructure.Reporting;

namespace AdaPlatform.Api.Endpoints;

/// <summary>Stop operations: how long vehicles stand at stops and what explains it.</summary>
public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        var operations = app.MapGroup("/api/operations").RequireAuthorization(Permissions.OperationsRead);

        // ?line=18 narrows everything to one line; without it, all lines. ?from=2022-08-01&to=2022-08-07
        // narrows it to trips starting on those days (both included; either end may be left open).
        operations.MapGet("/dwell", (StopDwellReport report, int? line, DateOnly? from, DateOnly? to, CancellationToken ct) =>
            report.GetAsync(line, new ReportPeriod(from, to), ct));
        // One stop's visits (stop detail) and one vehicle's day (trip strip).
        operations.MapGet("/dwell/stops/{code:int}", (StopDwellReport report, int code, int? line, DateOnly? from, DateOnly? to, CancellationToken ct) =>
            report.GetStopAsync(code, line, new ReportPeriod(from, to), ct));
        operations.MapGet("/vehicles/{vehicle:int}/days/{day}", (StopDwellReport report, int vehicle, DateOnly day, CancellationToken ct) =>
            report.GetVehicleDayAsync(vehicle, day, ct));

        // Punctuality (with passenger-weighted delay) and occupancy.
        operations.MapGet("/punctuality", (PunctualityReport report, int? line, DateOnly? from, DateOnly? to, CancellationToken ct) =>
            report.GetAsync(line, new ReportPeriod(from, to), ct));
        operations.MapGet("/load", (LoadReport report, int? line, int? pattern, DateOnly? from, DateOnly? to, CancellationToken ct) =>
            report.GetAsync(line, pattern, new ReportPeriod(from, to), ct));

        return app;
    }
}
