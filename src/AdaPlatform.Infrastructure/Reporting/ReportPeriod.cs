using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// The days a report covers, both ends included; a missing end is open. Trips belong to the day they
/// start on, so a trip that runs past midnight stays whole in one day.
/// </summary>
public readonly record struct ReportPeriod(DateOnly? From, DateOnly? To)
{
    public static readonly ReportPeriod All = new(null, null);

    /// <summary>First instant inside the period, or null when it has no start.</summary>
    public DateTime? Start => From?.ToDateTime(TimeOnly.MinValue);

    /// <summary>First instant after the period, or null when it has no end.</summary>
    public DateTime? End => To?.AddDays(1).ToDateTime(TimeOnly.MinValue);

    /// <summary>Part of a cache key.</summary>
    public string Key => $"{From?.ToString("yyyy-MM-dd") ?? "start"}..{To?.ToString("yyyy-MM-dd") ?? "end"}";

    /// <summary>Every day with a trip from the vehicle logs, for the period picker (whatever period is shown).</summary>
    public static async Task<IReadOnlyList<DateOnly>> DaysWithDataAsync(AppDbContext db, CancellationToken ct)
    {
        var days = await db.Trips.AsNoTracking().Where(t => t.SourceFileId != null)
            .Select(t => t.StartTime.Date).Distinct().OrderBy(d => d).ToListAsync(ct);
        return days.Select(DateOnly.FromDateTime).ToList();
    }
}
