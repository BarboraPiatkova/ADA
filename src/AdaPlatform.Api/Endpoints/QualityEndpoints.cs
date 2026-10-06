using AdaPlatform.Api.Auth;
using AdaPlatform.Infrastructure.Reporting;

namespace AdaPlatform.Api.Endpoints;

/// <summary>Device health and data quality; the reports cache themselves per data version.</summary>
public static class QualityEndpoints
{
    public static IEndpointRouteBuilder MapQualityEndpoints(this IEndpointRouteBuilder app)
    {
        var quality = app.MapGroup("/api/quality").RequireAuthorization(Permissions.QualityRead);

        // ?from=&to=&days= as on the operations screens: the log files of those service days only.
        quality.MapGet("/devices", (DeviceHealthReport report, DateOnly? from, DateOnly? to, string? days, CancellationToken ct) =>
            report.GetAsync(new ReportPeriod(from, to, ReportPeriod.ParseDays(days)), ct));
        quality.MapGet("/daily", (DailyQualityReport report, DateOnly? from, DateOnly? to, string? days, CancellationToken ct) =>
            report.GetAsync(new ReportPeriod(from, to, ReportPeriod.ParseDays(days)), ct));

        return app;
    }
}
