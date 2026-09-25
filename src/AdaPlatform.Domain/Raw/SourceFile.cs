namespace AdaPlatform.Domain.Raw;

/// <summary>
/// One ingested log file. The content hash makes ingestion idempotent: the same file
/// delivered twice (re-sent, or inside two archives) is recognised and skipped.
/// </summary>
public class SourceFile
{
    public long Id { get; set; }

    /// <summary>Where it came from, e.g. <c>APC_Logs.zip!1083/APC_1083.2022-08-01.csv</c>.</summary>
    public required string SourcePath { get; set; }

    public required string FileName { get; set; }
    public SourceFormat Format { get; set; }

    public int VehicleId { get; set; }

    /// <summary>Operating day the file covers (from its name).</summary>
    public DateOnly ServiceDate { get; set; }

    /// <summary>SHA-256 of the file bytes, lowercase hex.</summary>
    public required string Sha256 { get; set; }

    public long SizeBytes { get; set; }
    public int LineCount { get; set; }

    /// <summary>Lines that couldn't be parsed (e.g. NUL runs left by a power cut mid-write).</summary>
    public int MalformedLineCount { get; set; }

    /// <summary>UTC.</summary>
    public DateTime ImportedAt { get; set; }
}

public enum SourceFormat
{
    /// <summary>
    /// Per-vehicle daily log written by the on-board computer for UCP-01/UCP-02 units:
    /// <c>APC_&lt;vehicle&gt;.&lt;yyyy-MM-dd&gt;.csv</c>, 12 semicolon-separated columns.
    /// </summary>
    UcpLog,

    /// <summary>EyeOne counter log (tab-separated message types PC/PO/PP/PI/…). Not yet ingested.</summary>
    EyeOne,
}
