namespace AdaPlatform.Infrastructure.Import.Transportella;

/// <summary>Settings in the "Transportella:Statistics" section; which way the operations data arrives.</summary>
public sealed record TransportellaStatisticsOptions
{
    public const string SectionName = "Transportella:Statistics";

    /// <summary>Connection string name for <see cref="TransportellaStatisticsSourceKind.Database"/> (read-only account).</summary>
    public const string ConnectionStringName = "TransportellaStatistics";

    public TransportellaStatisticsSourceKind Source { get; init; } = TransportellaStatisticsSourceKind.None;

    /// <summary>The dump or XLSX file for the file-based sources.</summary>
    public string? Path { get; init; }

    /// <summary>Code page of a dump; Transportella's dumps are 852 (DOS Latin 2).</summary>
    public int DumpCodePage { get; init; } = 852;
}

public enum TransportellaStatisticsSourceKind
{
    None,

    /// <summary>Tab-separated dump of the Stat.Statistics table.</summary>
    Dump,

    /// <summary>Direct read of the Stat.Statistics table on Transportella's SQL Server.</summary>
    Database,

    /// <summary>The per-trip statistics report exported as XLSX.</summary>
    Report,
}
