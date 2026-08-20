using Microsoft.EntityFrameworkCore;

namespace AdaPlatform.Api.Data;

/// <summary>
/// Single EF Core context for the platform. Deliberately empty for now —
/// the first real migration adds Tenants + TenantId-scoped entities.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Tenant-scoped entities + global query filters land here in the next migration.
    }
}
