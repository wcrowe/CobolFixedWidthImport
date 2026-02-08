namespace CobolFixedWidthImport.Parsing.Config;

public static class ImportManifest
{
    public sealed record Manifest(List<ImportJob> Imports);

    public sealed record ImportJob(
        string Name,
        string InputPattern,
        string LayoutFile,
        string Mode,          // "single" or "graph"
        string? Entity,       // for single
        string? ParentEntity, // for graph
        string? SourceSystem,
        string? BatchId);
}
