using CobolFixedWidthImport.Import;
using CobolFixedWidthImport.Parsing.FixedWidth;

namespace CobolFixedWidthImport.Parsing.Mapping;

/// <summary>
/// Resolves where a field value comes from:
/// - fixedWidth slice (default)
/// - constant value (with ${BatchId}/${SourceSystem} tokens)
/// - now (UTC/local). For production, use context timestamp (shared per job).
/// </summary>
public sealed class ValueSourceResolver
{
    public object? Resolve(string line, FieldDefinition def, ImportContext ctx)
    {
        var source = GetString(def, "source", "fixedWidth").ToLowerInvariant();

        return source switch
        {
            "fixedwidth" => FixedWidthSlice.Slice(line, def.StartIndex0, def.Length),

            "constant" => ResolveConstant(def, ctx),

            "now" => ResolveNow(def, ctx),

            _ => FixedWidthSlice.Slice(line, def.StartIndex0, def.Length),
        };
    }

    private static object? ResolveConstant(FieldDefinition def, ImportContext ctx)
    {
        def.Options.TryGetValue("constantValue", out var v);
        if (string.IsNullOrWhiteSpace(v))
            return null;

        // Simple token replacement
        v = v.Replace("${BatchId}", ctx.BatchId, StringComparison.OrdinalIgnoreCase)
             .Replace("${SourceSystem}", ctx.SourceSystem, StringComparison.OrdinalIgnoreCase);

        return v;
    }

    private static object ResolveNow(FieldDefinition def, ImportContext ctx)
    {
        var kind = GetString(def, "nowKind", "utc").ToLowerInvariant();

        // Use shared per-job timestamp to keep all rows aligned
        return kind switch
        {
            "local" => ctx.ImportedAtUtc.ToLocalTime(),
            _ => ctx.ImportedAtUtc,
        };
    }

    private static string GetString(FieldDefinition def, string key, string fallback)
        => def.Options.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
}
