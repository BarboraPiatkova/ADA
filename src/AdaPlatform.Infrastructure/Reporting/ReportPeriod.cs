using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>Which days of the week a report counts: timetables differ between working days and weekends.</summary>
public enum DayKind
{
    All,
    Workdays,
    Saturday,
    Sunday,
}

/// <summary>
/// The days a report covers, both ends included; a missing end is open, and <see cref="Days"/> can keep
/// only working days, Saturdays or Sundays (public holidays count as the weekday they fall on). Trips
/// belong to the day they start on, so a trip that runs past midnight stays whole in one day.
/// </summary>
public readonly record struct ReportPeriod(DateOnly? From, DateOnly? To, DayKind Days = DayKind.All)
{
    public static readonly ReportPeriod All = new(null, null);

    /// <summary>First instant inside the period, or null when it has no start.</summary>
    public DateTime? Start => From?.ToDateTime(TimeOnly.MinValue);

    /// <summary>First instant after the period, or null when it has no end.</summary>
    public DateTime? End => To?.AddDays(1).ToDateTime(TimeOnly.MinValue);

    /// <summary>Part of a cache key.</summary>
    public string Key => $"{From?.ToString("yyyy-MM-dd") ?? "start"}..{To?.ToString("yyyy-MM-dd") ?? "end"}/{Days}";

    /// <summary>Whether a trip starting then falls on the kind of day kept (the date range is filtered in the query).</summary>
    public bool Keeps(DateTime tripStart) => Days switch
    {
        DayKind.Workdays => tripStart.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
        DayKind.Saturday => tripStart.DayOfWeek is DayOfWeek.Saturday,
        DayKind.Sunday => tripStart.DayOfWeek is DayOfWeek.Sunday,
        _ => true,
    };

    /// <summary>"workdays", "saturday" or "sunday" from a query string; anything else is every day.</summary>
    public static DayKind ParseDays(string? days) =>
        Enum.TryParse<DayKind>(days, ignoreCase: true, out var kind) ? kind : DayKind.All;

    /// <summary>ISO weekday: 1 = Monday … 7 = Sunday.</summary>
    public static int Weekday(DateTime at) => ((int)at.DayOfWeek + 6) % 7 + 1;

    /// <summary>Every day with a trip from the vehicle logs, for the period picker (whatever period is shown).</summary>
    public static async Task<IReadOnlyList<DateOnly>> DaysWithDataAsync(AppDbContext db, CancellationToken ct)
    {
        var days = await db.Trips.AsNoTracking().Where(t => t.SourceFileId != null)
            .Select(t => t.StartTime.Date).Distinct().OrderBy(d => d).ToListAsync(ct);
        return days.Select(DateOnly.FromDateTime).ToList();
    }
}
