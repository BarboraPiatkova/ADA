using AdaPlatform.Infrastructure.Persistence;
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
