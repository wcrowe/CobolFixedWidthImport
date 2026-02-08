namespace CobolFixedWidthImport.Parsing.FixedWidth;

public sealed record FieldDefinition(
    string Name,
    string Target,
    int StartIndex0,
    int Length,
    string Type,
    IReadOnlyDictionary<string, string> Options);
