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
            .IgnoreUnmatchedProperties()
            .Build();

    public async Task<ImportManifest.Manifest> LoadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Import manifest not found: {path}");
        var yaml = await YamlFile.ReadAllTextNoBomAsync(path, logger, ct);
        var manifest = Deserializer.Deserialize<ImportManifest.Manifest>(yaml)
                      ?? throw new InvalidOperationException("Failed to deserialize import manifest.");

        Validate(manifest);
        return manifest;
    }

    private static void Validate(ImportManifest.Manifest manifest)
    {
        foreach (var job in manifest.Imports)
        {
            if (string.IsNullOrWhiteSpace(job.Name)) throw new InvalidOperationException("Job name is required.");
            if (string.IsNullOrWhiteSpace(job.InputPattern)) throw new InvalidOperationException($"Job {job.Name}: inputPattern is required.");
            if (string.IsNullOrWhiteSpace(job.LayoutFile)) throw new InvalidOperationException($"Job {job.Name}: layoutFile is required.");
            if (string.IsNullOrWhiteSpace(job.Mode)) throw new InvalidOperationException($"Job {job.Name}: mode is required.");

            var mode = job.Mode.Trim().ToLowerInvariant();
            if (mode is not ("single" or "graph"))
                throw new InvalidOperationException($"Job {job.Name}: mode must be 'single' or 'graph'.");

            if (mode == "single" && string.IsNullOrWhiteSpace(job.Entity))
                throw new InvalidOperationException($"Job {job.Name}: mode=single requires entity.");

            if (mode == "graph" && string.IsNullOrWhiteSpace(job.ParentEntity))
                throw new InvalidOperationException($"Job {job.Name}: mode=graph requires parentEntity.");
        }
    }
}
