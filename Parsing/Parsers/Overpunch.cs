namespace CobolFixedWidthImport.Parsing.Parsers;

/// <summary>
/// ASCII overpunch mapping on last character:
/// Positive: { A..I => 0..9
/// Negative: } J..R => 0..9
/// </summary>
public static class Overpunch
{
    private static readonly Dictionary<char, (int Digit, int Sign)> Map = new()
    {
        ['{'] = (0, +1),
        ['A'] = (1, +1),
        ['B'] = (2, +1),
        ['C'] = (3, +1),
        ['D'] = (4, +1),
        ['E'] = (5, +1),
        ['F'] = (6, +1),
        ['G'] = (7, +1),
        ['H'] = (8, +1),
        ['I'] = (9, +1),

        ['}'] = (0, -1),
        ['J'] = (1, -1),
        ['K'] = (2, -1),
        ['L'] = (3, -1),
        ['M'] = (4, -1),
        ['N'] = (5, -1),
        ['O'] = (6, -1),
        ['P'] = (7, -1),
        ['Q'] = (8, -1),
        ['R'] = (9, -1),
    };

    public static bool TryDecode(char lastChar, out int digit, out int sign)
    {
        if (Map.TryGetValue(lastChar, out var v))
        {
            digit = v.Digit;
            sign = v.Sign;
            return true;
        }
        digit = 0;
        sign = +1;
        return false;
    }
}
