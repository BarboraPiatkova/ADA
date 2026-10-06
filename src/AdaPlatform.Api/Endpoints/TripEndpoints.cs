using AdaPlatform.Api.Auth;
using AdaPlatform.Infrastructure.Reporting;

namespace AdaPlatform.Api.Endpoints;

/// <summary>Every trip of a period, and one trip stop by stop. Under operations:read, like the other trip views.</summary>
public static class TripEndpoints
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder app)
    {
        var trips = app.MapGroup("/api/trips").RequireAuthorization(Permissions.OperationsRead);

        // ?line=&vehicle= narrow the list; ?from=&to=&days= as on the operations screens.
        trips.MapGet("/", (TripsReport report, int? line, int? vehicle, DateOnly? from, DateOnly? to, string? days, CancellationToken ct) =>
            report.GetAsync(line, vehicle, new ReportPeriod(from, to, ReportPeriod.ParseDays(days)), ct));
        trips.MapGet("/{id:long}", async (TripsReport report, long id, CancellationToken ct) =>
            await report.GetTripAsync(id, ct) is { } trip ? Results.Ok(trip) : Results.NotFound());

        return app;
    }
}
