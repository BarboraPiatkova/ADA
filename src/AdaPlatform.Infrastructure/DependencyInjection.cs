using AdaPlatform.Infrastructure.Fleet;
using AdaPlatform.Infrastructure.Import.Transportella;
using AdaPlatform.Infrastructure.Persistence;
using AdaPlatform.Infrastructure.Reconstruction;
using AdaPlatform.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AdaPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAdaPlatformDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));

        // Provider and connection string are resolved per DbContext instance (not captured
        // once at startup), so they always reflect the final merged configuration —
        // including overrides applied by integration tests.
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var provider = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.Provider;
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException(
                    "Connection string 'Default' is not configured. Set ConnectionStrings:Default " +
                    "(appsettings.Development.json for local dev, or the ConnectionStrings__Default " +
                    "environment variable when running via docker-compose).");
            options.UseAdaPlatformDatabase(provider, connectionString);
        });

        // Tied to the same DbContext/connection above — one source of truth for
        // "can we reach the database", whichever engine it is.
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>(name: "database");

        return services;
    }

    /// <summary>
    /// The quality reports, their thresholds (the "Quality:Thresholds" section; defaults in
    /// <see cref="HealthThresholds"/>) and the cache they keep their results in.
    /// </summary>
    public static IServiceCollection AddAdaPlatformReporting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HealthThresholds>().Bind(configuration.GetSection(HealthThresholds.SectionName));
        services.AddHybridCache();
        services.AddScoped<DeviceHealthReport>();
        services.AddScoped<DailyQualityReport>();
        services.AddScoped<DatasetProfiler>();
        return services;
    }

    /// <summary>Trip reconstruction from raw events and its settings (the "Reconstruction" section).</summary>
    public static IServiceCollection AddAdaPlatformReconstruction(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ReconstructionOptions>().Bind(configuration.GetSection(ReconstructionOptions.SectionName));
        services.AddScoped<TripReconstruction>();
        return services;
    }

    /// <summary>
    /// The fleet register the deployment is configured for ("Fleet:Source") and the sync that
    /// merges it into the platform's vehicles.
    /// </summary>
    public static IServiceCollection AddAdaPlatformFleet(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FleetOptions>().Bind(configuration.GetSection(FleetOptions.SectionName));
        services.AddHttpClient(nameof(AtlasFleetSource));
        services.AddScoped<IFleetSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<FleetOptions>>().Value;
            string FilePath() => options.Path is { Length: > 0 } path
                ? path
                : throw new InvalidOperationException($"Fleet:Path is required for Fleet:Source = {options.Source}.");
            return options.Source switch
            {
                FleetSourceKind.None => new NoFleetSource(),
                FleetSourceKind.EpisVehiclesXml => new EpisVehiclesXmlFleetSource(FilePath()),
                FleetSourceKind.Csv => new CsvFleetSource(FilePath()),
                FleetSourceKind.Atlas => new AtlasFleetSource(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(AtlasFleetSource)), options.Atlas),
                _ => throw new InvalidOperationException($"Unknown Fleet:Source '{options.Source}'."),
            };
        });
        services.AddScoped<FleetSync>();
        return services;
    }

    /// <summary>
    /// Transportella's per-stop operations (the "Transportella:Statistics" section): the configured
    /// source and the import. <see cref="CreateTransportellaSource"/> also serves a one-off file.
    /// </summary>
    public static IServiceCollection AddAdaPlatformTransportella(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TransportellaStatisticsOptions>().Bind(configuration.GetSection(TransportellaStatisticsOptions.SectionName));
        services.AddScoped<TransportellaStatisticsImporter>();
        return services;
    }

    /// <summary>
    /// The source for a file given on the command line (by extension: .xlsx = report, otherwise a dump),
    /// or the configured one when <paramref name="file"/> is null.
    /// </summary>
    public static ITransportellaStatisticsSource CreateTransportellaSource(this IServiceProvider services, string? file = null)
    {
        var options = services.GetRequiredService<IOptions<TransportellaStatisticsOptions>>().Value;
        var kind = file is null
            ? options.Source
            : file.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? TransportellaStatisticsSourceKind.Report : TransportellaStatisticsSourceKind.Dump;
        var path = file ?? options.Path;
        string RequirePath() => path is { Length: > 0 }
            ? path
            : throw new InvalidOperationException($"Transportella:Statistics:Path is required for source {kind}.");

        return kind switch
        {
            TransportellaStatisticsSourceKind.Dump => new TransportellaDumpSource(RequirePath(), DumpEncoding(options.DumpCodePage)),
            TransportellaStatisticsSourceKind.Report => new TransportellaReportXlsxSource(RequirePath()),
            TransportellaStatisticsSourceKind.Database => new TransportellaDatabaseSource(
                services.GetRequiredService<IConfiguration>().GetConnectionString(TransportellaStatisticsOptions.ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{TransportellaStatisticsOptions.ConnectionStringName}' is not configured (keep it in user-secrets or the environment).")),
            _ => throw new InvalidOperationException("No Transportella statistics source configured (Transportella:Statistics:Source)."),
        };

        static System.Text.Encoding DumpEncoding(int codePage)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            return System.Text.Encoding.GetEncoding(codePage);
        }
    }

    public static async Task MigrateAdaPlatformDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        if (!scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.MigrateOnStartup)
        {
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}
