#nullable enable
using System.ComponentModel.DataAnnotations;

namespace CobolFixedWidthImport.Parsing.Config;

public sealed record ImportManifest
{
    [Required]
    public required List<ImportJob> Jobs { get; init; }
}

public sealed record ImportJob
{
    [Required]
    public required string Name { get; init; }

    [Required]
    public required string Mode { get; init; } // "single" or "graph"

    [Required]
    public required string FileGlob { get; init; }

    [Required]
    public required string LayoutPath { get; init; }

    [Required]
    public required string TargetEntity { get; init; }

    public string? Table { get; init; }
}
