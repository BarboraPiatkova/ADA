using AdaPlatform.Api.Auth;
using AdaPlatform.Infrastructure.Reporting;

namespace AdaPlatform.Api.Endpoints;

/// <summary>Device health and data quality; the reports cache themselves per data version.</summary>
public static class QualityEndpoints
{
    public static IEndpointRouteBuilder MapQualityEndpoints(this IEndpointRouteBuilder app)
    {
        var quality = app.MapGroup("/api/quality").RequireAuthorization(Permissions.QualityRead);

        quality.MapGet("/devices", (DeviceHealthReport report, CancellationToken ct) => report.GetAsync(ct));
        quality.MapGet("/daily", (DailyQualityReport report, CancellationToken ct) => report.GetAsync(ct));

        return app;
    }
}
