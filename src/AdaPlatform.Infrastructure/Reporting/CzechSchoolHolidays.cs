namespace AdaPlatform.Infrastructure.Reporting;

/// <summary>
/// Czech school holidays as MŠMT publishes them ("Organizace školního roku", one document per school
/// year, č.j. MSMT-12071/2022-3, -4 and -5 for 2024/25 to 2026/27). Summer (1 July – 31 August) and the
/// Easter Thursday follow from vyhláška č. 16/2005 Sb.; autumn, Christmas and the half-term day are listed
/// per school year; the spring week depends on the school's district.
///
/// Spring break: the districts fall into six fixed groups whose weeks rotate by one each year, starting
/// on the first Monday of February (the group that went last goes first next year). Years MŠMT has not
/// published yet follow that rotation.
/// </summary>
public static class CzechSchoolHolidays
{
    /// <summary>Autumn, Christmas and half-term holidays per school year (keyed by the year it starts in).</summary>
    private static readonly Dictionary<int, (DateOnly From, DateOnly To)[]> Listed = new()
    {
        [2024] = [(new(2024, 10, 29), new(2024, 10, 30)), (new(2024, 12, 23), new(2025, 1, 3)), (new(2025, 1, 31), new(2025, 1, 31))],
        [2025] = [(new(2025, 10, 27), new(2025, 10, 29)), (new(2025, 12, 22), new(2026, 1, 2)), (new(2026, 1, 30), new(2026, 1, 30))],
        [2026] = [(new(2026, 10, 29), new(2026, 10, 30)), (new(2026, 12, 23), new(2027, 1, 3)), (new(2027, 1, 29), new(2027, 1, 29))],
    };

    /// <summary>The six spring-break groups, in the order of their weeks in the 2024/25 school year.</summary>
    private static readonly string[][] SpringGroups =
    [
        ["Benešov", "Beroun", "Rokycany", "České Budějovice", "Český Krumlov", "Klatovy", "Trutnov", "Pardubice", "Chrudim", "Svitavy", "Ústí nad Orlicí", "Ostrava-město", "Prostějov"],
        ["Praha 1 až 5", "Blansko", "Brno-město", "Brno-venkov", "Břeclav", "Hodonín", "Vyškov", "Znojmo", "Domažlice", "Tachov", "Louny", "Karviná"],
        ["Praha 6 až 10", "Cheb", "Karlovy Vary", "Sokolov", "Nymburk", "Jindřichův Hradec", "Litoměřice", "Děčín", "Přerov", "Frýdek-Místek"],
        ["Kroměříž", "Uherské Hradiště", "Vsetín", "Zlín", "Praha-východ", "Praha-západ", "Mělník", "Rakovník", "Plzeň-město", "Plzeň-sever", "Plzeň-jih", "Hradec Králové", "Teplice", "Nový Jičín"],
        ["Česká Lípa", "Jablonec nad Nisou", "Liberec", "Semily", "Havlíčkův Brod", "Jihlava", "Pelhřimov", "Třebíč", "Žďár nad Sázavou", "Kladno", "Kolín", "Kutná Hora", "Písek", "Náchod", "Bruntál"],
        ["Mladá Boleslav", "Příbram", "Tábor", "Prachatice", "Strakonice", "Ústí nad Labem", "Chomutov", "Most", "Jičín", "Rychnov nad Kněžnou", "Olomouc", "Šumperk", "Opava", "Jeseník"],
    ];

    public static IEnumerable<string> Districts => SpringGroups.SelectMany(g => g);

    public static bool IsKnownDistrict(string district) => GroupOf(district) is not null;

    /// <summary>Whether the day is a school holiday; the spring week only when the district is known.</summary>
    public static bool IsHoliday(DateOnly day, string? district)
    {
        if (day.Month is 7 or 8)
        {
            return true;
        }
        var schoolYear = day.Month >= 9 ? day.Year : day.Year - 1;
        if (Listed.TryGetValue(schoolYear, out var ranges) && ranges.Any(r => day >= r.From && day <= r.To))
        {
            return true;
        }
        // Easter holidays: the Thursday before Good Friday.
        if (day == DayCalendar.EasterSunday(day.Year).AddDays(-3))
        {
            return true;
        }
        return district is not null && SpringWeek(day.Year, district) is { } monday && day >= monday && day <= monday.AddDays(6);
    }

    /// <summary>Monday of the district's spring week in that calendar year, or null for an unknown district.</summary>
    public static DateOnly? SpringWeek(int year, string district)
    {
        if (GroupOf(district) is not { } group)
        {
            return null;
        }
        var firstMonday = new DateOnly(year, 2, 1);
        while (firstMonday.DayOfWeek != DayOfWeek.Monday)
        {
            firstMonday = firstMonday.AddDays(1);
        }
        // 2025 (school year 2024/25): group 0 first. Each year every group moves one week later, the last to the front.
        var week = ((group + year - 2025) % 6 + 6) % 6;
        return firstMonday.AddDays(7 * week);
    }

    private static int? GroupOf(string district)
    {
        for (var g = 0; g < SpringGroups.Length; g++)
        {
            if (SpringGroups[g].Any(d => string.Equals(d, district.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return g;
            }
        }
        return null;
    }
}
