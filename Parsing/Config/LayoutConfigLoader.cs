using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CobolFixedWidthImport.Parsing.Config;

public sealed class LayoutConfigLoader
{
    private static readonly IDeserializer Deserializer =
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

    public async Task<LayoutConfig.Layout> LoadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Layout config not found: {path}");

        var yaml = await File.ReadAllTextAsync(path, ct);
        var parsed = Deserializer.Deserialize<LayoutConfig.Layout>(yaml)
                     ?? throw new InvalidOperationException("Failed to deserialize YAML layout config.");

        // Fix: OccursGroups is init-only (positional record). Create a new Layout with a non-null list.
        var occursGroups = parsed.OccursGroups ?? new List<LayoutConfig.OccursGroupSpec>();
        var layout = new LayoutConfig.Layout(parsed.HeaderFields, occursGroups, parsed.Rules);

        Validate(layout);
        return layout;
    }


    private static void Validate(LayoutConfig.Layout layout)
    {
        if (layout.HeaderFields is null || layout.HeaderFields.Count == 0)
            throw new InvalidOperationException("layout.headerFields must contain at least one field.");

        foreach (var f in layout.HeaderFields)
            ValidateField(f, "headerFields");

        foreach (var g in layout.OccursGroups)
        {
            if (string.IsNullOrWhiteSpace(g.Name)) throw new InvalidOperationException("occursGroups[].name is required.");
            if (string.IsNullOrWhiteSpace(g.ParentCollectionTarget)) throw new InvalidOperationException($"occursGroups[{g.Name}].parentCollectionTarget is required.");
            if (string.IsNullOrWhiteSpace(g.ChildEntity)) throw new InvalidOperationException($"occursGroups[{g.Name}].childEntity is required.");

            if (g.Start <= 0 || g.Length <= 0 || g.ItemLength <= 0 || g.MaxItems <= 0)
                throw new InvalidOperationException($"occursGroups[{g.Name}] start/length/itemLength/maxItems must be > 0.");

            if (g.ItemFields is null || g.ItemFields.Count == 0)
                throw new InvalidOperationException($"occursGroups[{g.Name}].itemFields must contain at least one field.");

            foreach (var f in g.ItemFields)
                ValidateField(f, $"occursGroups[{g.Name}].itemFields");

            var mode = (g.TerminationMode ?? "padding").Trim().ToLowerInvariant();
            if (mode is not ("padding" or "count"))
                throw new InvalidOperationException($"occursGroups[{g.Name}].terminationMode must be 'padding' or 'count'.");

            if (mode == "count" && string.IsNullOrWhiteSpace(g.CountFieldTarget))
                throw new InvalidOperationException($"occursGroups[{g.Name}] terminationMode=count requires countFieldTarget.");

            if (g.Sequence?.Enabled == true && string.IsNullOrWhiteSpace(g.Sequence.Target))
                throw new InvalidOperationException($"occursGroups[{g.Name}].sequence.target is required when enabled.");
        }
    }

    private static void ValidateField(LayoutConfig.FieldSpec f, string where)
    {
        if (string.IsNullOrWhiteSpace(f.Name)) throw new InvalidOperationException($"{where}: field name is required.");
        if (string.IsNullOrWhiteSpace(f.Target)) throw new InvalidOperationException($"{where}: field '{f.Name}' target is required.");
        if (string.IsNullOrWhiteSpace(f.Type)) throw new InvalidOperationException($"{where}: field '{f.Name}' type is required.");

        // start/length are ignored for non-fixedWidth sources, but still keep basic sanity
        if (f.Start < 1) throw new InvalidOperationException($"{where}: field '{f.Name}' start must be >= 1.");
        if (f.Length < 0) throw new InvalidOperationException($"{where}: field '{f.Name}' length must be >= 0.");
    }
}
