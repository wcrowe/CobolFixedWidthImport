using System.Globalization;
using CobolFixedWidthImport.Parsing.Config;
using CobolFixedWidthImport.Parsing.FixedWidth;

namespace CobolFixedWidthImport.Parsing.Parsers;

public sealed class NumericFieldParser : IFieldParser
{
    public object? Parse(string raw, FieldDefinition field, LayoutConfig.ParsingRules rules)
    {
        var treatSpacesAsNull = GetBool(field, "treatAllSpacesAsNull", rules.NumericFields.TreatAllSpacesAsNull);
        if (treatSpacesAsNull && ParsingHelpers.IsAllSpaces(raw))
            return null;

        var allZerosBehavior = GetString(field, "allZerosBehavior", rules.NumericFields.AllZerosBehavior);
        if (ParsingHelpers.IsAllZeros(raw))
            return allZerosBehavior.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : 0m;

        var allowOverpunch = GetBool(field, "allowOverpunch", rules.NumericFields.AllowOverpunch);
        var impliedPlaces = GetInt(field, "impliedDecimalPlaces", rules.NumericFields.DefaultImpliedDecimalPlaces);

        var s = ParsingHelpers.CollapseSpaces(raw);
        if (string.IsNullOrWhiteSpace(s))
            return null;

        var sign = +1;

        // Explicit sign
        if (s.Length > 0 && (s[0] == '+' || s[0] == '-'))
        {
            if (s[0] == '-') sign = -1;
            s = s[1..];
        }

        // Overpunch on last char
        if (allowOverpunch && s.Length > 0)
        {
            var last = s[^1];
            if (Overpunch.TryDecode(last, out var digit, out var opSign))
            {
                sign *= opSign;
                s = s[..^1] + digit.ToString(CultureInfo.InvariantCulture);
            }
        }

        // If decimal point exists: parse as-is (keeps “current” decimals as value)
        if (s.Contains('.', StringComparison.Ordinal))
        {
            if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var decExplicit))
                throw new FormatException($"Invalid numeric value for field '{field.Name}': '{raw}'");

            return decExplicit * sign;
        }

        // Implied decimals
        var digits = new string(s.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return null;

        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var integral))
            throw new FormatException($"Invalid numeric digits for field '{field.Name}': '{raw}'");

        var dec = impliedPlaces <= 0
            ? (decimal)integral
            : integral / (decimal)Math.Pow(10, impliedPlaces);

        return dec * sign;
    }

    private static bool GetBool(FieldDefinition field, string key, bool fallback)
        => field.Options.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

    private static int GetInt(FieldDefinition field, string key, int fallback)
        => field.Options.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fallback;

    private static string GetString(FieldDefinition field, string key, string fallback)
        => field.Options.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
}
