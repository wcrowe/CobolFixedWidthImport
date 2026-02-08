using CobolFixedWidthImport.Parsing.Config;
using CobolFixedWidthImport.Parsing.FixedWidth;

namespace CobolFixedWidthImport.Parsing.Parsers;

public sealed class BooleanFieldParser : IFieldParser
{
    public object? Parse(string raw, FieldDefinition field, LayoutConfig.ParsingRules rules)
    {
        var anyNonBlankIsTrue = GetBool(field, "anyNonBlankIsTrue", rules.BooleanFields.AnyNonBlankIsTrue);
        var allSpacesBehavior = GetString(field, "allSpacesBehavior", rules.BooleanFields.AllSpacesBehavior);

        if (ParsingHelpers.IsAllSpaces(raw))
        {
            return allSpacesBehavior.ToLowerInvariant() switch
            {
                "null" => (bool?)null,
                "false" => false,
                "true" => true,
                _ => (bool?)null
            };
        }

        var s = raw.Trim();

        if (anyNonBlankIsTrue)
            return true;

        var trueVals = GetList(field, "trueValues", rules.BooleanFields.TrueValues);
        var falseVals = GetList(field, "falseValues", rules.BooleanFields.FalseValues);

        if (trueVals.Any(v => string.Equals(v, s, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (falseVals.Any(v => string.Equals(v, s, StringComparison.OrdinalIgnoreCase)))
            return false;

        throw new FormatException($"Invalid boolean value for field '{field.Name}': '{raw}'");
    }

    private static bool GetBool(FieldDefinition field, string key, bool fallback)
        => field.Options.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

    private static string GetString(FieldDefinition field, string key, string fallback)
        => field.Options.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

    private static IReadOnlyList<string> GetList(FieldDefinition field, string key, IReadOnlyList<string> fallback)
    {
        if (field.Options.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
            return v.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return fallback;
    }
}
