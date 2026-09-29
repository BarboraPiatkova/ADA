using AdaPlatform.Infrastructure.Import.Epcomp;

namespace AdaPlatform.Api.Tests;

/// <summary>Stop names take EPComp's diacritics only where they differ from ours in nothing else.</summary>
public sealed class EpcompStationsTests
{
    [Theory]
    [InlineData("Namesti Miru", "Náměstí Míru", "Náměstí Míru")]
    [InlineData("Sokolnice,Brnenska", "Sokolnice, Brněnská", null)]   // a missing space is more than an accent
    [InlineData("Sokolnice , Brnenska", "Sokolnice, Brněnská", "Sokolnice, Brněnská")]
    [InlineData("", "Akátky", "Akátky")]
    [InlineData("Hlavni nadrazi", "Hlavní nádraží", "Hlavní nádraží")]
    [InlineData("Malinovskeho namesti", "Malinovského náměstí", "Malinovského náměstí")]
    [InlineData("Stara osada", "Stará osada", "Stará osada")]
    [InlineData("Mendlovo namesti", "Úzká", null)]   // renamed or wrong: kept, reported
    [InlineData("Náměstí Míru", "Náměstí Míru", null)]   // already right
    public void Only_accents_are_corrected(string ours, string theirs, string? result) =>
        Assert.Equal(result, EpcompStationsImporter.Better(ours, theirs));

    [Fact]
    public void Route_ends_take_the_stop_names_and_keep_a_leading_code()
    {
        var names = new Dictionary<string, string>
        {
            [EpcompStationsImporter.Fold("Komín, smyčka")] = "Komín, smyčka",
            [EpcompStationsImporter.Fold("Purmerendská")] = "Purmerendská",
        };
        Assert.Equal("Komín, smyčka", EpcompStationsImporter.Renamed("Komin, smycka", names));
        Assert.Equal("14901 Purmerendská", EpcompStationsImporter.Renamed("14901 Purmerendska", names));
        Assert.Equal("Brafova", EpcompStationsImporter.Renamed("Brafova", names));
        Assert.Null(EpcompStationsImporter.Renamed(null, names));
    }
}
