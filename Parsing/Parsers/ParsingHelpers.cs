using System.Globalization;

namespace CobolFixedWidthImport.Parsing.Parsers;

public static class ParsingHelpers
{
    public static bool IsAllSpaces(string s) => s.All(c => c == ' ');

    public static bool IsAllZeros(string s)
    {
        foreach (var c in s)
        {
            if (c == ' ') return false;
            if (c != '0' && c != '.') return false;
        }
        return s.Any(ch => ch == '0');
    }

    public static string CollapseSpaces(string s) => s.Replace(" ", "", StringComparison.Ordinal);

    public static string ApplyTrim(string raw, string trimMode)
        => trimMode.ToLowerInvariant() switch
        {
            "left" => raw.TrimStart(),
            "right" => raw.TrimEnd(),
            "both" => raw.Trim(),
            "none" => raw,
            _ => raw.Trim()
        };

    public static string ApplyCase(string s, string caseMode)
        => caseMode.ToLowerInvariant() switch
        {
            "upper" => s.ToUpperInvariant(),
            "lower" => s.ToLowerInvariant(),
            "none" => s,
            _ => s
        };

    public static bool TryParseExactDate(string input, IReadOnlyList<string> formats, out DateTime value)
    {
        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(input, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
                return true;
        }
        value = default;
        return false;
    }
}
