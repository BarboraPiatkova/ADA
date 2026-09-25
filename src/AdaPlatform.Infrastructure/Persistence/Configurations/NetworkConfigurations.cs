using AdaPlatform.Domain.Network;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdaPlatform.Infrastructure.Persistence.Configurations;

// Reference entities keep the operator's natural keys — they come from the timetable,
// not from this database, so they're never generated here.

internal sealed class StopConfiguration : IEntityTypeConfiguration<Stop>
{
    public void Configure(EntityTypeBuilder<Stop> builder)
    {
        builder.HasKey(s => s.Code);
        builder.Property(s => s.Code).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.Tariffs).HasMaxLength(100);
    }
}

internal sealed class LineConfiguration : IEntityTypeConfiguration<Line>
{
    public void Configure(EntityTypeBuilder<Line> builder)
    {
        builder.Property(l => l.Id).ValueGeneratedNever();
    }
}

internal sealed class PatternConfiguration : IEntityTypeConfiguration<Pattern>
{
    public void Configure(EntityTypeBuilder<Pattern> builder)
    {
        builder.HasKey(p => p.Code);
        builder.Property(p => p.Code).ValueGeneratedNever();
        builder.Property(p => p.FirstStopName).HasMaxLength(200);
        builder.Property(p => p.LastStopName).HasMaxLength(200);

        builder.HasOne(p => p.Line)
            .WithMany(l => l.Patterns)
            .HasForeignKey(p => p.LineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PatternStopConfiguration : IEntityTypeConfiguration<PatternStop>
{
    public void Configure(EntityTypeBuilder<PatternStop> builder)
    {
        builder.HasKey(ps => new { ps.PatternCode, ps.Sequence });

        builder.HasOne(ps => ps.Pattern)
            .WithMany(p => p.Stops)
            .HasForeignKey(ps => ps.PatternCode)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ps => ps.Stop)
            .WithMany()
            .HasForeignKey(ps => ps.StopCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BlockConfiguration : IEntityTypeConfiguration<Block>
{
    public void Configure(EntityTypeBuilder<Block> builder)
    {
        builder.HasKey(b => b.Code);
        builder.Property(b => b.Code).HasMaxLength(20);
    }
}
