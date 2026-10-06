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
        // narrows it to trips starting on those days (both included; either end may be left open), and
        // ?days=workdays|schoolWorkdays|holidayWorkdays|saturday|sundayOrHoliday|publicHoliday to those days
        // (public and school holidays: DayCalendar).
        operations.MapGet("/dwell", (StopDwellReport report, int? line, DateOnly? from, DateOnly? to, string? days, CancellationToken ct) =>
            report.GetAsync(line, Period(from, to, days), ct));
        // One stop's visits (stop detail) and one vehicle's day (trip strip).
        operations.MapGet("/dwell/stops/{code:int}", (StopDwellReport report, int code, int? line, DateOnly? from, DateOnly? to, string? days, CancellationToken ct) =>
            report.GetStopAsync(code, line, Period(from, to, days), ct));
        operations.MapGet("/vehicles/{vehicle:int}/days/{day}", (StopDwellReport report, int vehicle, DateOnly day, CancellationToken ct) =>
            report.GetVehicleDayAsync(vehicle, day, ct));

        // Punctuality (with passenger-weighted delay) and occupancy.
        // ?times=transportella takes the times from Transportella's recorded calls instead of the vehicles' logs.
        operations.MapGet("/punctuality", (PunctualityReport report, int? line, DateOnly? from, DateOnly? to, string? days, string? times, CancellationToken ct) =>
            report.GetAsync(line, Period(from, to, days), Times(times), ct));
        operations.MapGet("/load", (LoadReport report, int? line, int? pattern, DateOnly? from, DateOnly? to, string? days, CancellationToken ct) =>
            report.GetAsync(line, pattern, Period(from, to, days), ct));

        return app;
    }

    private static ReportPeriod Period(DateOnly? from, DateOnly? to, string? days) => new(from, to, ReportPeriod.ParseDays(days));

    private static TimesSource Times(string? times) =>
        Enum.TryParse<TimesSource>(times, ignoreCase: true, out var source) ? source : TimesSource.VehicleLog;
}
