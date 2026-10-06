using AdaPlatform.Infrastructure.Reporting;
using Microsoft.Extensions.Options;

namespace AdaPlatform.Api.Tests;

/// <summary>Czech public holidays (Easter included) and the kinds of day a timetable runs on.</summary>
public sealed class DayCalendarTests
{
    private static DayCalendar Calendar(params HolidayRange[] schoolHolidays) =>
        new(Options.Create(new CalendarOptions { District = "Brno-město", SchoolHolidays = [.. schoolHolidays] }));

    [Theory]
    [InlineData(2022, 4, 17)]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    public void Easter_Sunday_is_computed(int year, int month, int day) =>
        Assert.Equal(new DateOnly(year, month, day), DayCalendar.EasterSunday(year));

    [Theory]
    [InlineData("2026-01-01", true)]
    [InlineData("2026-04-03", true)]    // Good Friday
    [InlineData("2026-04-06", true)]    // Easter Monday
    [InlineData("2026-04-05", false)]   // Easter Sunday is a Sunday, not a public holiday
    [InlineData("2026-09-28", true)]
    [InlineData("2026-11-17", true)]
    [InlineData("2026-12-24", true)]
    [InlineData("2026-09-29", false)]
    [InlineData("2015-04-03", false)]   // Good Friday became a holiday in 2016
    public void Public_holidays_are_recognised(string day, bool holiday) =>
        Assert.Equal(holiday, Calendar().IsPublicHoliday(DateOnly.Parse(day)));

    // MŠMT, Organizace školního roku 2024/25 – 2026/27.
    [Theory]
    [InlineData("2025-10-27", true)]    // autumn
    [InlineData("2025-10-28", true)]    // a public holiday in between, still no school
    [InlineData("2025-10-30", false)]
    [InlineData("2025-12-22", true)]    // Christmas
    [InlineData("2026-01-02", true)]
    [InlineData("2026-01-05", false)]
    [InlineData("2026-01-30", true)]    // half-term day
    [InlineData("2026-04-02", true)]    // Easter Thursday
    [InlineData("2026-02-16", true)]    // Brno-město's spring week 2026 …
    [InlineData("2026-02-22", true)]
    [InlineData("2026-02-09", false)]   // … not the week before
    [InlineData("2027-02-22", true)]    // 2027, one week later
    [InlineData("2025-02-10", true)]    // 2025, one week earlier
    [InlineData("2022-08-03", true)]    // summer
    [InlineData("2026-09-01", false)]
    public void School_holidays_come_from_the_ministry(string day, bool holiday) =>
        Assert.Equal(holiday, Calendar().IsSchoolHoliday(DateOnly.Parse(day)));

    [Theory]
    [InlineData(2025, "Olomouc", "2025-03-10")]
    [InlineData(2026, "Olomouc", "2026-02-02")]
    [InlineData(2027, "Olomouc", "2027-02-08")]
    [InlineData(2026, "Praha 6 až 10", "2026-02-23")]
    [InlineData(2027, "jihlava", "2027-02-01")]
    public void The_spring_week_rotates_through_the_district_groups(int year, string district, string monday) =>
        Assert.Equal(DateOnly.Parse(monday), CzechSchoolHolidays.SpringWeek(year, district));

    [Fact]
    public void An_unknown_district_has_no_spring_week()
    {
        Assert.Null(CzechSchoolHolidays.SpringWeek(2026, "Atlantis"));
        Assert.False(CzechSchoolHolidays.IsKnownDistrict("Atlantis"));
    }

    [Fact]
    public void Days_are_kept_by_the_timetable_they_run()
    {
        // The operator ran its holiday timetable on two extra days.
        var calendar = Calendar(new HolidayRange(new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 22)));
        var termMonday = new DateTime(2026, 9, 14, 8, 0, 0);
        var extraHoliday = new DateTime(2026, 9, 21, 8, 0, 0);
        var summerMonday = new DateTime(2026, 8, 3, 8, 0, 0);
        var christmasMonday = new DateTime(2025, 12, 29, 8, 0, 0);
        var statehoodDay = new DateTime(2026, 9, 28, 8, 0, 0);   // a Monday
        var saturday = new DateTime(2026, 9, 26, 8, 0, 0);

        Assert.True(calendar.Keeps(DayKind.SchoolWorkdays, termMonday));
        Assert.False(calendar.Keeps(DayKind.HolidayWorkdays, termMonday));
        Assert.True(calendar.Keeps(DayKind.HolidayWorkdays, summerMonday));
        Assert.True(calendar.Keeps(DayKind.HolidayWorkdays, christmasMonday));
        Assert.True(calendar.Keeps(DayKind.HolidayWorkdays, extraHoliday));

        // A public holiday on a Monday runs the Sunday timetable, not a working day's.
        Assert.False(calendar.Keeps(DayKind.Workdays, statehoodDay));
        Assert.True(calendar.Keeps(DayKind.SundayOrHoliday, statehoodDay));
        Assert.True(calendar.Keeps(DayKind.PublicHoliday, statehoodDay));

        Assert.True(calendar.Keeps(DayKind.Saturday, saturday));
        Assert.False(calendar.Keeps(DayKind.SundayOrHoliday, saturday));
        Assert.True(calendar.Keeps(DayKind.All, saturday));
    }

    [Theory]
    [InlineData("sundayOrHoliday", DayKind.SundayOrHoliday)]
    [InlineData("SCHOOLWORKDAYS", DayKind.SchoolWorkdays)]
    [InlineData("nonsense", DayKind.All)]
    [InlineData(null, DayKind.All)]
    public void The_kind_of_day_is_read_from_the_query(string? text, DayKind kind) =>
        Assert.Equal(kind, ReportPeriod.ParseDays(text));
}
