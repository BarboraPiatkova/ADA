using AdaPlatform.Domain.Fleet;
using AdaPlatform.Domain.Raw;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdaPlatform.Infrastructure.Persistence.Configurations;

internal sealed class SourceFileConfiguration : IEntityTypeConfiguration<SourceFile>
{
    public void Configure(EntityTypeBuilder<SourceFile> builder)
    {
        builder.Property(f => f.SourcePath).HasMaxLength(500);
        builder.Property(f => f.FileName).HasMaxLength(260);
        builder.Property(f => f.Format).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Sha256).HasMaxLength(64).IsFixedLength();

        // Idempotent ingestion: the same bytes are never ingested twice.
        builder.HasIndex(f => f.Sha256).IsUnique();
        builder.HasIndex(f => new { f.VehicleId, f.ServiceDate });

        builder.HasOne<Vehicle>().WithMany()
            .HasForeignKey(f => f.VehicleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DeviceEventConfiguration : IEntityTypeConfiguration<DeviceEvent>
{
    public void Configure(EntityTypeBuilder<DeviceEvent> builder)
    {
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.BlockCode).HasMaxLength(20);
        builder.Property(e => e.StatusRegister).HasMaxLength(20);
        builder.Property(e => e.FirmwareVersion).HasMaxLength(50);
        builder.Property(e => e.InvalidDevices).HasMaxLength(100);
        builder.Property(e => e.Payload).HasMaxLength(1000);

        // Deleting a source file (e.g. to re-ingest it) removes its events with it.
        builder.HasOne(e => e.SourceFile).WithMany()
            .HasForeignKey(e => e.SourceFileId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SourceFileId, e.LineNumber }).IsUnique();

        // Trip reconstruction reads a vehicle's day in order; fault detection reads
        // one device over time.
        builder.HasIndex(e => new { e.VehicleId, e.Time });
        builder.HasIndex(e => new { e.VehicleId, e.DeviceNumber, e.Time });
        builder.HasIndex(e => e.Type);
    }
}
