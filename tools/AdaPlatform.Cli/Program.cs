// AdaPlatform command-line tool.
//
//   dotnet run --project tools/AdaPlatform.Cli -- import-ada --source C:\Projects\ADA\ADA.dbFile
//   dotnet run --project tools/AdaPlatform.Cli -- import-ucp --source T:\Projects\DPMB\ADA\ADA_20220808\APC_Logs.zip
//   dotnet run --project tools/AdaPlatform.Cli -- reconstruct
//   dotnet run --project tools/AdaPlatform.Cli -- sync-fleet
//   dotnet run --project tools/AdaPlatform.Cli -- import-transportella --source <dump or .xlsx> [--from 2026-09-01] [--to 2026-09-30]
//   dotnet run --project tools/AdaPlatform.Cli -- import-stations --source <EPComp export>\General\stations.xml
//
// Target engine and connection come from appsettings.json / environment, exactly as for the API.

using AdaPlatform.Infrastructure;
using AdaPlatform.Infrastructure.Fleet;
using AdaPlatform.Infrastructure.Import.Ada;
using AdaPlatform.Infrastructure.Import.Epcomp;
using AdaPlatform.Infrastructure.Import.Transportella;
using AdaPlatform.Infrastructure.Import.Ucp;
using AdaPlatform.Infrastructure.Persistence;
using AdaPlatform.Infrastructure.Reconstruction;
using AdaPlatform.Infrastructure.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

const string Usage = """
    Usage:
      AdaPlatform.Cli import-ada --source <ADA.dbFile>          seed from a legacy ADA database (empty target only)
      AdaPlatform.Cli import-ucp --source <folder or .zip>      ingest raw UCP logs (APC_*.csv) and reconstruct their trips; safe to re-run
      AdaPlatform.Cli reconstruct                               rebuild every trip from the raw logs (after a rule change)
      AdaPlatform.Cli sync-fleet                                update vehicles from the fleet register (Fleet:Source)
      AdaPlatform.Cli import-transportella [--source <file>] [--from <date>] [--to <date>] [--carrier <name>]
                                                                load Transportella's per-stop operations (a dump, an .xlsx report,
                                                                or a .zip/folder of daily service reports; else the configured
                                                                source); --carrier keeps one carrier of a regional system; safe to re-run
      AdaPlatform.Cli import-stations --source <stations.xml>   take stop names with diacritics (and missing positions) from
                                                                EPComp's stop list; safe to re-run
      AdaPlatform.Cli profile --out <folder>                    write the thesis data report (Markdown + CSV)
    """;

if (args.Length == 0)
{
    Console.Error.WriteLine(Usage);
    return 1;
}

var command = args[0];

// Content root = the build output, so appsettings.json is found wherever it's run from.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args[1..],
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Services.AddAdaPlatformDatabase(builder.Configuration);
builder.Services.AddAdaPlatformReporting(builder.Configuration);
builder.Services.AddAdaPlatformReconstruction(builder.Configuration);
builder.Services.AddAdaPlatformFleet(builder.Configuration);
builder.Services.AddAdaPlatformTransportella(builder.Configuration);
using var host = builder.Build();

var source = builder.Configuration["source"];
var output = builder.Configuration["out"];
var missingArgument = command switch
{
    "profile" => string.IsNullOrWhiteSpace(output),
    "reconstruct" or "sync-fleet" or "import-transportella" => false,
    _ => string.IsNullOrWhiteSpace(source),
};
if (missingArgument)
{
    Console.Error.WriteLine(Usage);
    return 1;
}

await host.Services.MigrateAdaPlatformDatabaseAsync();
await using var scope = host.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

try
{
    object report = command switch
    {
        "import-ada" => await new AdaSqliteImporter(db).ImportAsync(source!),
        // New logs get their trips straight away; files already reconstructed are left alone.
        "import-ucp" => $"{await new UcpLogIngestor(db).IngestAsync(source!)}\n{await Reconstruction().ReconstructAsync(onlyNew: true)}",
        "reconstruct" => await Reconstruction().ReconstructAsync(),
        "sync-fleet" => await scope.ServiceProvider.GetRequiredService<FleetSync>().SyncAsync(),
        "import-transportella" => await scope.ServiceProvider.GetRequiredService<TransportellaStatisticsImporter>().ImportAsync(
            scope.ServiceProvider.CreateTransportellaSource(source, builder.Configuration["carrier"]), Day("from"), Day("to")),
        "import-stations" => await new EpcompStationsImporter(db).ImportAsync(source!),
        "profile" => await scope.ServiceProvider.GetRequiredService<DatasetProfiler>().ProfileAsync(output!, CodeVersion()),
        _ => throw new ArgumentException($"Unknown command '{command}'."),
    };
    Console.WriteLine(report);
    return 0;
}
catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(Usage);
    return 2;
}

DateOnly? Day(string name) => builder.Configuration[name] is { Length: > 0 } value
    ? DateOnly.ParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
    : null;

TripReconstruction Reconstruction() => scope.ServiceProvider.GetRequiredService<TripReconstruction>();

// Git commit of the code that computed the figures, so the report says which version produced it.
static string CodeVersion()
{
    try
    {
        var git = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", "describe --always --dirty")
        {
            RedirectStandardOutput = true,
            WorkingDirectory = Environment.CurrentDirectory,
        })!;
        var version = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return git.ExitCode == 0 && version.Length > 0 ? version : "unknown";
    }
    catch (System.ComponentModel.Win32Exception)
    {
        return "unknown";
    }
}
