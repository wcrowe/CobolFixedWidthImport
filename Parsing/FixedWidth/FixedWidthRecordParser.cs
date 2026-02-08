using System.Reflection;
using CobolFixedWidthImport.Import;
using CobolFixedWidthImport.Parsing.Config;
using CobolFixedWidthImport.Parsing.Mapping;
using CobolFixedWidthImport.Parsing.Parsers;

namespace CobolFixedWidthImport.Parsing.FixedWidth;

/// <summary>
/// Parses a record line into:
/// - single entity (mode=single), or
/// - parent entity with multiple OCCURS child collections (mode=graph)
/// All mapping is YAML-driven (no switch statements).
/// </summary>
public sealed class FixedWidthRecordParser(
    ParserFactory parserFactory,
    EntityTypeRegistry typeRegistry,
    ValueSourceResolver valueSource,
    PropertySetterCache setterCache,
    CollectionAdderCache adderCache)
{
    public object ParseSingle(string line, Type entityType, LayoutConfig.Layout layout, ImportContext ctx)
    {
        var entity = Activator.CreateInstance(entityType)
            ?? throw new InvalidOperationException($"Failed to create instance of {entityType.Name}");

        ApplyFields(entity, entityType, line, layout.HeaderFields, layout, ctx);

        // ignore occursGroups for single mode by design
        return entity;
    }

    public object ParseGraph(string line, Type parentType, LayoutConfig.Layout layout, ImportContext ctx)
    {
        var parent = Activator.CreateInstance(parentType)
            ?? throw new InvalidOperationException($"Failed to create instance of {parentType.Name}");

        ApplyFields(parent, parentType, line, layout.HeaderFields, layout, ctx);

        foreach (var group in layout.OccursGroups)
        {
            var childType = typeRegistry.Resolve(group.ChildEntity);
            var addChild = adderCache.GetAdder(parentType, group.ParentCollectionTarget, childType);

            var groupBlock = FixedWidthSlice.Slice(line, group.Start - 1, group.Length);

            var itemsToParse = DetermineItemsToParse(group, parent);
            var maxBytes = Math.Min(groupBlock.Length, group.ItemLength * group.MaxItems);

            for (var i = 0; i < itemsToParse; i++)
            {
                var offset = i * group.ItemLength;
                if (offset >= maxBytes) break;

                var itemRaw = FixedWidthSlice.Slice(groupBlock, offset, group.ItemLength);

                if (group.TerminationMode.Equals("padding", StringComparison.OrdinalIgnoreCase) &&
                    ParsingHelpers.IsAllSpaces(itemRaw))
                {
                    break;
                }

                var child = Activator.CreateInstance(childType)
                    ?? throw new InvalidOperationException($"Failed to create instance of {childType.Name}");

                // Assign child fields from the item block (item field start is relative to itemRaw)
                ApplyFieldsFromItemBlock(child, childType, itemRaw, group.ItemFields, layout, ctx);

                // Optional per-group sequence generation with configurable property name
                if (group.Sequence?.Enabled == true)
                {
                    var seqSetter = setterCache.GetSetter(childType, group.Sequence.Target);
                    var seqValue = group.Sequence.Start + (i * group.Sequence.Step);
                    seqSetter(child, seqValue);
                }

                addChild(parent, child);
            }
        }

        return parent;
    }

    private void ApplyFields(object instance, Type instanceType, string line, List<LayoutConfig.FieldSpec> specs, LayoutConfig.Layout layout, ImportContext ctx)
    {
        foreach (var def in ToFieldDefinitions(specs))
        {
            var resolved = valueSource.Resolve(line, def, ctx);

            object? finalValue;
            if (IsFixedWidth(def) && resolved is string rawSlice)
            {
                var parser = parserFactory.Create(def, layout.Rules);
                finalValue = parser.Parse(rawSlice, def, layout.Rules);
            }
            else
            {
                finalValue = resolved;
            }

            var setter = setterCache.GetSetter(instanceType, def.Target);
            setter(instance, finalValue);
        }
    }

    private void ApplyFieldsFromItemBlock(object instance, Type instanceType, string itemRaw, List<LayoutConfig.FieldSpec> specs, LayoutConfig.Layout layout, ImportContext ctx)
    {
        foreach (var def in ToFieldDefinitions(specs))
        {
            // For item blocks, fixedWidth slices come from itemRaw, not the whole line.
            object? resolved;
            if (IsFixedWidth(def))
            {
                resolved = FixedWidthSlice.Slice(itemRaw, def.StartIndex0, def.Length);
            }
            else
            {
                // constant/now still allowed on child rows
                resolved = valueSource.Resolve(line: "", def, ctx);
            }

            object? finalValue;
            if (IsFixedWidth(def) && resolved is string rawSlice)
            {
                var parser = parserFactory.Create(def, layout.Rules);
                finalValue = parser.Parse(rawSlice, def, layout.Rules);
            }
            else
            {
                finalValue = resolved;
            }

            var setter = setterCache.GetSetter(instanceType, def.Target);
            setter(instance, finalValue);
        }
    }

    private static int DetermineItemsToParse(LayoutConfig.OccursGroupSpec group, object parent)
    {
        var mode = (group.TerminationMode ?? "padding").Trim().ToLowerInvariant();

        if (mode == "count")
        {
            if (string.IsNullOrWhiteSpace(group.CountFieldTarget))
                throw new InvalidOperationException($"occursGroups[{group.Name}] terminationMode=count requires countFieldTarget.");

            var prop = parent.GetType().GetProperty(
                           group.CountFieldTarget,
                           BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                       ?? throw new InvalidOperationException(
                           $"CountFieldTarget '{group.CountFieldTarget}' not found on parent type '{parent.GetType().Name}'.");

            var val = prop.GetValue(parent);

            var count = val switch
            {
                null => 0,
                int i => i,
                long l => checked((int)l),
                string s when int.TryParse(s.Trim(), out var parsed) => parsed, // optional
                _ => throw new InvalidOperationException(
                    $"CountFieldTarget '{group.CountFieldTarget}' must be int/long (or parsable string). Actual: {val.GetType().FullName}")
            };

            return Math.Clamp(count, 0, group.MaxItems);
        }

        // padding mode scans up to MaxItems (and stops on all-spaces item)
        return group.MaxItems;
    }

    private static bool IsFixedWidth(FieldDefinition def)
        => !def.Options.TryGetValue("source", out var src) || string.IsNullOrWhiteSpace(src) ||
           src.Equals("fixedWidth", StringComparison.OrdinalIgnoreCase);

    private static List<FieldDefinition> ToFieldDefinitions(List<LayoutConfig.FieldSpec> specs)
        => specs.Select(s => new FieldDefinition(
                Name: s.Name,
                Target: s.Target,
                StartIndex0: s.Start - 1,
                Length: s.Length,
                Type: s.Type,
                Options: s.Options ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
            .ToList();
}
