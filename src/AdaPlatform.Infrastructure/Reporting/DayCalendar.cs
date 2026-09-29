using Microsoft.Extensions.Options;

namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// The operator's calendar settings. The national school holidays come with the platform
/// (<see cref="CzechSchoolHolidays"/>); an operator only names its district for the spring week, and may
/// add days it ran a holiday timetable on anyway (Operations:Calendar).
/// </summary>
public sealed class CalendarOptions
{
    public const string SectionName = "Operations:Calendar";

    /// <summary>The district (okres, or "Praha 1 až 5" / "Praha 6 až 10") whose spring week counts, e.g. "Brno-město".</summary>
    public string? District { get; init; }

    /// <summary>Further holiday days, both ends included, e.g. { "From": "2025-12-29", "To": "2025-12-31" }.</summary>
    public List<HolidayRange> SchoolHolidays { get; init; } = [];
}

public sealed record HolidayRange(DateOnly From, DateOnly To);

/// <summary>
/// The kinds of day a timetable runs on: Czech public holidays (computed, Easter included) and school
/// holidays (<see cref="CzechSchoolHolidays"/> plus <see cref="CalendarOptions"/>). Public transport runs its Sunday timetable on public
/// holidays and a holiday timetable on working days during school holidays.
/// </summary>
public sealed class DayCalendar(IOptions<CalendarOptions> options)
{
    // State holidays and other public holidays with a fixed date (zákon č. 245/2000 Sb.).
    private static readonly (int Month, int Day)[] FixedHolidays =
        [(1, 1), (5, 1), (5, 8), (7, 5), (7, 6), (9, 28), (10, 28), (11, 17), (12, 24), (12, 25), (12, 26)];

    public bool IsPublicHoliday(DateOnly day)
    {
        if (FixedHolidays.Contains((day.Month, day.Day)))
        {
            return true;
        }
        var easter = EasterSunday(day.Year);
        // Easter Monday; Good Friday since 2016.
        return day == easter.AddDays(1) || (day.Year >= 2016 && day == easter.AddDays(-2));
    }

    public bool IsSchoolHoliday(DateOnly day) =>
        CzechSchoolHolidays.IsHoliday(day, options.Value.District)
        || options.Value.SchoolHolidays.Any(h => day >= h.From && day <= h.To);

    /// <summary>A working day: Monday to Friday and not a public holiday.</summary>
    public bool IsWorkday(DateOnly day) => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !IsPublicHoliday(day);

    /// <summary>Whether a trip starting then falls on the kind of day kept.</summary>
    public bool Keeps(DayKind kind, DateTime tripStart)
    {
        var day = DateOnly.FromDateTime(tripStart);
        return kind switch
        {
            DayKind.Workdays => IsWorkday(day),
            DayKind.SchoolWorkdays => IsWorkday(day) && !IsSchoolHoliday(day),
            DayKind.HolidayWorkdays => IsWorkday(day) && IsSchoolHoliday(day),
            DayKind.Saturday => day.DayOfWeek is DayOfWeek.Saturday && !IsPublicHoliday(day),
            DayKind.SundayOrHoliday => day.DayOfWeek is DayOfWeek.Sunday || IsPublicHoliday(day),
            DayKind.PublicHoliday => IsPublicHoliday(day),
            _ => true,
        };
    }

    /// <summary>Easter Sunday in the Gregorian calendar (the anonymous Gregorian algorithm).</summary>
    public static DateOnly EasterSunday(int year)
    {
        int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4;
        int f = (b + 8) / 25, g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var dayOfMonth = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, dayOfMonth);
    }
}
