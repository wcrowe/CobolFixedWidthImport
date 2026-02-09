#nullable enable
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CobolFixedWidthImport.Parsing.Config;

public sealed class ImportManifestLoader(ILogger<ImportManifestLoader> logger)
{
    private readonly ILogger _logger = logger;

    private static readonly IDeserializer Deserializer =
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

    public async Task<ImportManifest> LoadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Manifest not found: {path}");

        var yaml = await File.ReadAllTextAsync(path, ct);

        if (string.IsNullOrWhiteSpace(yaml))
            throw new InvalidOperationException("Manifest YAML is empty.");

        try
        {
            var manifest = Deserializer.Deserialize<ImportManifest>(yaml)
                ?? throw new InvalidOperationException("Manifest deserialized as null.");

            if (manifest.Jobs is null || manifest.Jobs.Count == 0)
                throw new InvalidOperationException("Manifest contains no jobs.");

            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YAML parse failure: {Path}", path);
            throw;
        }
    }
}
