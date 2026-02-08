using System.Globalization;
using CobolFixedWidthImport.Parsing.Config;
using CobolFixedWidthImport.Parsing.FixedWidth;

namespace CobolFixedWidthImport.Parsing.Parsers;

public sealed class IntegerFieldParser : IFieldParser
{
    public object? Parse(string raw, FieldDefinition field, LayoutConfig.ParsingRules rules)
    {
        var treatSpacesAsNull = GetBool(field, "treatAllSpacesAsNull", rules.IntegerFields.TreatAllSpacesAsNull);
        if (treatSpacesAsNull && ParsingHelpers.IsAllSpaces(raw))
            return null;

        var allZerosBehavior = GetString(field, "allZerosBehavior", rules.IntegerFields.AllZerosBehavior);
        if (ParsingHelpers.IsAllZeros(raw))
            return allZerosBehavior.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : 0L;

        var s = ParsingHelpers.CollapseSpaces(raw);
        if (string.IsNullOrWhiteSpace(s))
            return null;

        var sign = +1;
        if (s.Length > 0 && (s[0] == '+' || s[0] == '-'))
        {
            if (s[0] == '-') sign = -1;
            s = s[1..];
        }

        var digits = new string(s.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return null;

        if (digits.Length is < 1 or > 11)
            throw new FormatException($"Integer field '{field.Name}' must be 1-11 digits. Raw='{raw}'");

        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Invalid integer value for field '{field.Name}': '{raw}'");

        return value * sign;
    }

    private static bool GetBool(FieldDefinition field, string key, bool fallback)
        => field.Options.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

    private static string GetString(FieldDefinition field, string key, string fallback)
        => field.Options.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
}
