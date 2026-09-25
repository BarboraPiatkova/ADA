using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AdaPlatform.Migrations.Postgres;

/// <summary>
/// Lets `dotnet ef migrations add` build the model for Postgres without starting the API.
/// Generating a migration never connects, so the connection string is a placeholder.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAdaPlatformDatabase(DatabaseProvider.Postgres, "Host=localhost;Database=design_time");
        return new AppDbContext(options.Options);
    }
}
