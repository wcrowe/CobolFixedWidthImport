namespace CobolFixedWidthImport.Parsing.Config;

/// <summary>
/// Layout config for a single copybook/file. Positions are 1-based in YAML.
/// </summary>
public static class LayoutConfig
{
    public sealed record Layout(
        List<FieldSpec> HeaderFields,
        List<OccursGroupSpec> OccursGroups,
        ParsingRules Rules);

    public sealed record FieldSpec(
        string Name,
        string Target, // property name/path on entity
        int Start,
        int Length,
        string Type,
        Dictionary<string, string>? Options);

    public sealed record SequenceSpec(
        bool Enabled,
        string Target,  // property name/path on child entity (per-table configurable)
        int Start,
        int Step);

    public sealed record OccursGroupSpec(
        string Name,
        string ParentCollectionTarget, // property name/path on parent entity
        string ChildEntity,            // entity type name (registry)
        int Start,
        int Length,
        int ItemLength,
        int MaxItems,
        string TerminationMode,        // "padding" or "count"
        string? CountFieldTarget,      // parent property containing count
        SequenceSpec? Sequence,
        List<FieldSpec> ItemFields);

    public sealed record ParsingRules(
        DateRules DateFields,
        NumericRules NumericFields,
        IntegerRules IntegerFields,
        StringRules StringFields,
        BooleanRules BooleanFields);

    public sealed record DateRules(
        List<string> Formats,
        bool TreatAllZerosAsNull,
        bool TreatAllSpacesAsNull);

    public sealed record NumericRules(
        bool AllowOverpunch,
        bool TreatAllSpacesAsNull,
        string AllZerosBehavior, // "null" or "zero"
        int DefaultImpliedDecimalPlaces);

    public sealed record IntegerRules(
        bool TreatAllSpacesAsNull,
        string AllZerosBehavior); // "null" or "zero"

    public sealed record StringRules(
        string DefaultTrim,          // left/right/both/none
        string AllSpacesBehavior,    // null/empty/keep
        string CaseNormalization,    // upper/lower/none
        Dictionary<string, string>? Replacements);

    public sealed record BooleanRules(
        List<string> TrueValues,
        List<string> FalseValues,
        bool AnyNonBlankIsTrue,
        string AllSpacesBehavior); // null/false/true
}
