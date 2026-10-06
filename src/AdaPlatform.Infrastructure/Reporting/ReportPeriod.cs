using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// Which days a report counts, as timetables run: working days (in term or in school holidays),
/// Saturdays, Sundays with public holidays (they run the Sunday timetable), or public holidays alone.
/// See <see cref="DayCalendar"/>.
/// </summary>
public enum DayKind
{
    All,
    Workdays,
    SchoolWorkdays,
    HolidayWorkdays,
    Saturday,
    SundayOrHoliday,
    PublicHoliday,
}

/// <summary>
/// The days a report covers, both ends included; a missing end is open, and <see cref="Days"/> keeps only
/// one kind of day (judged by <see cref="DayCalendar"/>). Trips
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

    /// <summary>"workdays", "schoolWorkdays", "holidayWorkdays", "saturday", "sundayOrHoliday" or "publicHoliday"
    /// from a query string (any case); anything else is every day.</summary>
    public static DayKind ParseDays(string? days) =>
        Enum.TryParse<DayKind>(days, ignoreCase: true, out var kind) ? kind : DayKind.All;

    /// <summary>ISO weekday: 1 = Monday … 7 = Sunday.</summary>
    public static int Weekday(DateTime at) => ((int)at.DayOfWeek + 6) % 7 + 1;

    /// <summary>Whether the period narrows anything at all.</summary>
    public bool IsAll => From is null && To is null && Days == DayKind.All;

    /// <summary>
    /// The vehicle log files of the period (one file = one vehicle's service day), or null for every file.
    /// Reports over raw device events filter by these, so the kind of day is judged per service day.
    /// </summary>
    public async Task<IReadOnlyCollection<long>?> SourceFilesAsync(AppDbContext db, DayCalendar calendar, CancellationToken ct)
    {
        if (IsAll)
        {
            return null;
        }
        var files = db.SourceFiles.AsNoTracking();
        if (From is { } from)
        {
            files = files.Where(f => f.ServiceDate >= from);
        }
        if (To is { } to)
        {
            files = files.Where(f => f.ServiceDate <= to);
        }
        var days = Days;
        return (await files.Select(f => new { f.Id, f.ServiceDate }).ToListAsync(ct))
            .Where(f => calendar.Keeps(days, f.ServiceDate.ToDateTime(TimeOnly.MinValue)))
            .Select(f => f.Id)
            .ToHashSet();
    }

    /// <summary>Every service day with a vehicle log, for the period picker on the device screens.</summary>
    public static async Task<IReadOnlyList<DateOnly>> ServiceDaysAsync(AppDbContext db, CancellationToken ct) =>
        await db.SourceFiles.AsNoTracking().Select(f => f.ServiceDate).Distinct().OrderBy(d => d).ToListAsync(ct);

    /// <summary>Every day with a trip from the vehicle logs, for the period picker (whatever period is shown).</summary>
    public static async Task<IReadOnlyList<DateOnly>> DaysWithDataAsync(AppDbContext db, CancellationToken ct)
    {
        var days = await db.Trips.AsNoTracking().Where(t => t.SourceFileId != null)
            .Select(t => t.StartTime.Date).Distinct().OrderBy(d => d).ToListAsync(ct);
        return days.Select(DateOnly.FromDateTime).ToList();
    }
}
