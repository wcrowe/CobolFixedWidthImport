namespace CobolFixedWidthImport.Parsing.FixedWidth;

public static class FixedWidthSlice
{
    public static string Slice(string line, int startIndex0, int length)
    {
        if (length <= 0) return string.Empty;
        if (startIndex0 < 0) startIndex0 = 0;

        if (startIndex0 >= line.Length)
            return new string(' ', length);

        var remaining = line.Length - startIndex0;
        if (remaining >= length)
            return line.Substring(startIndex0, length);

        var partial = line.Substring(startIndex0, remaining);
        return partial.PadRight(length, ' ');
    }
}
