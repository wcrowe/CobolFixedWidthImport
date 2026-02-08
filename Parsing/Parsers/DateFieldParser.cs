using CobolFixedWidthImport.Parsing.Config;
using CobolFixedWidthImport.Parsing.FixedWidth;

namespace CobolFixedWidthImport.Parsing.Parsers;

public sealed class DateFieldParser : IFieldParser
{
    public object? Parse(string raw, FieldDefinition field, LayoutConfig.ParsingRules rules)
    {
        var formats = GetFormats(field, rules);

        if (rules.DateFields.TreatAllSpacesAsNull && ParsingHelpers.IsAllSpaces(raw))
            return null;

        var collapsed = ParsingHelpers.CollapseSpaces(raw);
        if (string.IsNullOrWhiteSpace(collapsed))
            return null;

        if (rules.DateFields.TreatAllZerosAsNull && collapsed.All(c => c == '0'))
            return null;

        if (ParsingHelpers.TryParseExactDate(collapsed, formats, out var dt))
            return dt;

        if (DateTime.TryParse(collapsed, out var dt2))
            return dt2;

        throw new FormatException($"Invalid date value for field '{field.Name}': '{raw}'");
    }

    private static IReadOnlyList<string> GetFormats(FieldDefinition field, LayoutConfig.ParsingRules rules)
    {
        if (field.Options.TryGetValue("formats", out var fmt) && !string.IsNullOrWhiteSpace(fmt))
            return fmt.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return rules.DateFields.Formats;
    }
}
