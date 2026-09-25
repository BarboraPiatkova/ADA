using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Infrastructure.Persistence;

/// <summary>The relational databases the platform can run on. See ADR 0003.</summary>
public enum DatabaseProvider
{
    Postgres,
    SqlServer,
}

/// <summary>Bound from the "Database" configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Postgres;

    /// <summary>
    /// Apply pending migrations when the API starts. Convenient for a single on-prem
    /// deployment; turn off where a DBA applies the migration scripts by hand.
    /// </summary>
    public bool MigrateOnStartup { get; set; }
}

public static class DatabaseProviderExtensions
{
    /// <summary>
    /// Each provider keeps its own migrations in its own assembly: the generated SQL
    /// and column types differ, so one shared migration set can't serve both.
    /// </summary>
    public static string MigrationsAssembly(this DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Postgres => "AdaPlatform.Migrations.Postgres",
        DatabaseProvider.SqlServer => "AdaPlatform.Migrations.SqlServer",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    /// <summary>
    /// The single place that turns a provider choice into EF Core configuration —
    /// used by the API at runtime and by each migrations project at design time.
    /// </summary>
    public static DbContextOptionsBuilder UseAdaPlatformDatabase(
        this DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString)
    {
        var migrationsAssembly = provider.MigrationsAssembly();
        return provider switch
        {
            DatabaseProvider.Postgres => options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsAssembly(migrationsAssembly)),
            DatabaseProvider.SqlServer => options.UseSqlServer(connectionString,
                sqlServer => sqlServer.MigrationsAssembly(migrationsAssembly)),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
        };
    }
}
