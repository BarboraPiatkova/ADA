using AdaPlatform.Domain.Operations;
using AdaPlatform.Domain.Quality;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdaPlatform.Infrastructure.Persistence.Configurations;

internal sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.Property(t => t.Origin).HasConversion<string>().HasMaxLength(20);

        // Restrict, not cascade: a trip references Vehicles twice, and SQL Server
        // rejects multiple cascade paths to the same table.
        builder.HasOne(t => t.Vehicle).WithMany()
            .HasForeignKey(t => t.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.SecondVehicle).WithMany()
            .HasForeignKey(t => t.SecondVehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Pattern).WithMany()
            .HasForeignKey(t => t.PatternCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Block).WithMany()
            .HasForeignKey(t => t.BlockCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.SourceFile).WithMany()
            .HasForeignKey(t => t.SourceFileId).OnDelete(DeleteBehavior.Restrict);

        // The access paths every KPI and gap-filling query uses.
        builder.HasIndex(t => t.StartTime);
        builder.HasIndex(t => new { t.PatternCode, t.StartTime });
        builder.HasIndex(t => new { t.VehicleId, t.StartTime });
    }
}

internal sealed class StopVisitConfiguration : IEntityTypeConfiguration<StopVisit>
{
    public void Configure(EntityTypeBuilder<StopVisit> builder)
    {
        builder.Property(s => s.Origin).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(s => s.Trip)
            .WithMany(t => t.StopVisits)
            .HasForeignKey(s => s.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Stop).WithMany()
            .HasForeignKey(s => s.StopCode).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.TripId, s.Sequence }).IsUnique();
        builder.HasIndex(s => s.StopCode);
    }
}

internal sealed class DoorCountConfiguration : IEntityTypeConfiguration<DoorCount>
{
    public void Configure(EntityTypeBuilder<DoorCount> builder)
    {
        builder.HasKey(d => new { d.StopVisitId, d.CountingDeviceId });
        builder.Property(d => d.Origin).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(d => d.StopVisit)
            .WithMany(s => s.DoorCounts)
            .HasForeignKey(d => d.StopVisitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.CountingDevice).WithMany()
            .HasForeignKey(d => d.CountingDeviceId).OnDelete(DeleteBehavior.Restrict);

        // Per-device history for fault detection.
        builder.HasIndex(d => d.CountingDeviceId);
    }
}

internal sealed class RecordedCallConfiguration : IEntityTypeConfiguration<RecordedCall>
{
    public void Configure(EntityTypeBuilder<RecordedCall> builder)
    {
        builder.Property(c => c.Source).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.VehicleCode).HasMaxLength(30);
        builder.Property(c => c.Line).HasMaxLength(30);
        builder.Property(c => c.LineCourse).HasMaxLength(100);
        builder.Property(c => c.TripNumber).HasMaxLength(20);
        builder.Property(c => c.StopName).HasMaxLength(255);
        builder.Property(c => c.Traction).HasMaxLength(30);

        // No foreign keys: a dispatch system knows vehicles and stops before, or without, any log.
        builder.HasIndex(c => new { c.Source, c.ExternalId }).IsUnique();
        // The join to the counting data: vehicle and time, then stop.
        builder.HasIndex(c => new { c.VehicleId, c.ActualArrival });
        builder.HasIndex(c => new { c.StationId, c.PlannedDeparture });
    }
}

internal sealed class DeviceFaultConfiguration : IEntityTypeConfiguration<DeviceFault>
{
    public void Configure(EntityTypeBuilder<DeviceFault> builder)
    {
        builder.Property(f => f.Kind).HasConversion<string>().HasMaxLength(40);
        builder.Property(f => f.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Details).HasMaxLength(500);

        builder.HasOne(f => f.Trip).WithMany()
            .HasForeignKey(f => f.TripId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(f => new { f.VehicleId, f.DeviceNumber, f.From });
    }
}
