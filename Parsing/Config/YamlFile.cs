using System.Text;
using Microsoft.Extensions.Logging;

namespace CobolFixedWidthImport.Parsing.Config;

internal static class YamlFile
{
    public static async Task<string> ReadAllTextNoBomAsync(string path, ILogger logger, CancellationToken ct)
    {
        var fi = new FileInfo(path);
        logger.LogInformation("Loading YAML: {Path} (Exists={Exists}, Length={Length})",
            fi.FullName, fi.Exists, fi.Exists ? fi.Length : -1);

        if (!fi.Exists)
            throw new FileNotFoundException($"YAML file not found: {fi.FullName}");

        var bytes = await File.ReadAllBytesAsync(fi.FullName, ct);

        // Strip UTF-8 BOM if present
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            bytes = bytes[3..];

        var text = Encoding.UTF8.GetString(bytes);

        // Also strip rare leading NUL/whitespace junk
        text = text.TrimStart('\u0000', '\uFEFF');

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"YAML file is empty or whitespace: {fi.FullName}");

        return text;
    }
}