using CobolFixedWidthImport.Parsing.Config;
using CobolFixedWidthImport.Parsing.FixedWidth;

namespace CobolFixedWidthImport.Parsing.Parsers;

public sealed class StringFieldParser : IFieldParser
{
    public object? Parse(string raw, FieldDefinition field, LayoutConfig.ParsingRules rules)
    {
        var trimMode = GetString(field, "trim", rules.StringFields.DefaultTrim);
        var allSpacesBehavior = GetString(field, "allSpacesBehavior", rules.StringFields.AllSpacesBehavior);
        var caseMode = GetString(field, "case", rules.StringFields.CaseNormalization);

        if (ParsingHelpers.IsAllSpaces(raw))
        {
            return allSpacesBehavior.ToLowerInvariant() switch
            {
                "null" => null,
                "empty" => string.Empty,
                "keep" => raw,
                _ => null
            };
        }

        var s = ParsingHelpers.ApplyTrim(raw, trimMode);
        s = ParsingHelpers.ApplyCase(s, caseMode);

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        if (rules.StringFields.Replacements is not null)
        {
            foreach (var kv in rules.StringFields.Replacements)
                merged[kv.Key] = kv.Value;
        }

        if (field.Options.TryGetValue("replacements", out var repStr) && !string.IsNullOrWhiteSpace(repStr))
        {
            foreach (var pair in repStr.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var idx = pair.IndexOf('=');
                if (idx > 0)
                    merged[pair[..idx]] = pair[(idx + 1)..];
            }
        }

        foreach (var kv in merged)
            s = s.Replace(kv.Key, kv.Value, StringComparison.Ordinal);

        return s;
    }

    private static string GetString(FieldDefinition field, string key, string fallback)
        => field.Options.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
}
