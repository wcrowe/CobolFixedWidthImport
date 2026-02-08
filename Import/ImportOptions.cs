using System.ComponentModel.DataAnnotations;

namespace CobolFixedWidthImport.Import;

public sealed class ImportOptions
{
    [Required]
    public string InputDirectory { get; init; } = "Input";

    [Range(1, 100_000)]
    public int ChunkSize { get; init; } = 2000;

    [Required]
    public string ManifestPath { get; init; } = "Config/import-manifest.yaml";
}
