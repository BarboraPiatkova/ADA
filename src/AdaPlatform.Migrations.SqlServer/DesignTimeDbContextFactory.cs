using AdaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AdaPlatform.Migrations.SqlServer;

/// <summary>
/// Lets `dotnet ef migrations add` build the model for SQL Server without starting the API.
/// Generating a migration never connects, so the connection string is a placeholder.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAdaPlatformDatabase(DatabaseProvider.SqlServer, "Server=localhost;Database=design_time;TrustServerCertificate=True");
        return new AppDbContext(options.Options);
    }
}
