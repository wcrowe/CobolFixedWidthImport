namespace CobolFixedWidthImport.Import;

/// <summary>
/// Per import-job context. Important: ImportedAtUtc is shared across all rows for that job.
/// </summary>
public sealed record ImportContext(
    DateTime ImportedAtUtc,
    string SourceSystem,
    string BatchId);
