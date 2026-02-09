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
        var yaml = await YamlFile.ReadAllTextNoBomAsync(path, _logger, ct);

        try
        {
            var manifest = Deserializer.Deserialize<ImportManifest>(yaml)
                ?? throw new InvalidOperationException("Manifest deserialized as null.");

            Validate(manifest);
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse manifest YAML.");
            throw;
        }
    }

    private static void Validate(ImportManifest manifest)
    {
        if (manifest.Jobs is null || manifest.Jobs.Count == 0)
            throw new InvalidOperationException("Manifest contains no jobs.");

        foreach (var job in manifest.Jobs)
        {
            if (string.IsNullOrWhiteSpace(job.FileGlob))
                throw new InvalidOperationException($"Job '{job.Name}' missing FileGlob.");
        }
    }
}
