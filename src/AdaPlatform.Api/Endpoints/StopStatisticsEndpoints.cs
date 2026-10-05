using AdaPlatform.Api.Auth;
using AdaPlatform.Infrastructure.Reporting;

namespace AdaPlatform.Api.Endpoints;

/// <summary>Boardings, alightings and load per stop post, and one stop by line, hour and weekday. Under operations:read.</summary>
public static class StopStatisticsEndpoints
{
    public static IEndpointRouteBuilder MapStopStatisticsEndpoints(this IEndpointRouteBuilder app)
    {
        var stops = app.MapGroup("/api/stop-statistics").RequireAuthorization(Permissions.OperationsRead);

        // ?line=&vehicle= narrow the trips; ?trips=all counts invalid trips and depot runs too; ?from=&to=&days= as elsewhere.
        stops.MapGet("/", (StopStatisticsReport report, int? line, int? vehicle, string? trips, DateOnly? from, DateOnly? to, string? days, CancellationToken ct) =>
            report.GetAsync(Filter(line, vehicle, trips, from, to, days), ct));
        stops.MapGet("/{code:int}", async (StopStatisticsReport report, int code, int? line, int? vehicle, string? trips, DateOnly? from, DateOnly? to, string? days, CancellationToken ct) =>
            await report.GetStopAsync(code, Filter(line, vehicle, trips, from, to, days), ct) is { } stop ? Results.Ok(stop) : Results.NotFound());

        return app;
    }

    private static StopFilter Filter(int? line, int? vehicle, string? trips, DateOnly? from, DateOnly? to, string? days) =>
        new(line, vehicle, string.Equals(trips, "all", StringComparison.OrdinalIgnoreCase), new ReportPeriod(from, to, ReportPeriod.ParseDays(days)));
}
