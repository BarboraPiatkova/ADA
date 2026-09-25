// AdaPlatform command-line tool.
//
//   dotnet run --project tools/AdaPlatform.Cli -- import-ada --source C:\Projects\ADA\ADA.dbFile
//
// Target engine and connection come from appsettings.json / environment, exactly as for the API.

using AdaPlatform.Infrastructure;
using AdaPlatform.Infrastructure.Import.Ada;
using AdaPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

const string Usage = """
    Usage:
      AdaPlatform.Cli import-ada --source <ADA.dbFile>          seed from a legacy ADA database (empty target only)
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
using var host = builder.Build();

var source = builder.Configuration["source"];
if (string.IsNullOrWhiteSpace(source))
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

