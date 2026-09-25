using AdaPlatform.Domain.Fleet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdaPlatform.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Depot).HasMaxLength(50);
        builder.Property(v => v.Traction).HasMaxLength(50);
        builder.Property(v => v.Model).HasMaxLength(100);
        builder.Ignore(v => v.TotalCapacity);
    }
}

internal sealed class CountingDeviceConfiguration : IEntityTypeConfiguration<CountingDevice>
{
    public void Configure(EntityTypeBuilder<CountingDevice> builder)
    {
        builder.Property(d => d.FirmwareVersion).HasMaxLength(50);

        builder.HasOne(d => d.Vehicle)
            .WithMany(v => v.CountingDevices)
            .HasForeignKey(d => d.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.VehicleId, d.DeviceNumber }).IsUnique();
    }
}
