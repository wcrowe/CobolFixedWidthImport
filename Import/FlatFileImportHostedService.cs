#nullable enable
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CobolFixedWidthImport.Parsing.Config;

namespace CobolFixedWidthImport.Import;

public sealed class FlatFileImportHostedService(
    ILogger<FlatFileImportHostedService> logger,
    ImportManifestLoader manifestLoader,
    IOptions<ImportOptions> options)
    : BackgroundService
{
    private readonly ILogger _logger = logger;
    private readonly ImportManifestLoader _manifestLoader = manifestLoader;
    private readonly ImportOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("InputDirectory: {Dir}", _options.InputDirectory);
        _logger.LogInformation("ManifestPath: {Manifest}", _options.ManifestPath);
        _logger.LogInformation("ChunkSize: {Chunk}", _options.ChunkSize);

        ImportManifest manifest;

        try
        {
            manifest = await _manifestLoader.LoadAsync(_options.ManifestPath, ct);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to load manifest. Stopping host.");
            throw;
        }

        foreach (var job in manifest.Jobs)
        {
            ct.ThrowIfCancellationRequested();

            await ProcessJobAsync(job, ct);
        }

        _logger.LogInformation("All import jobs complete.");
    }

    private async Task ProcessJobAsync(ImportJob job, CancellationToken ct)
    {
        _logger.LogInformation("Starting job: {Job}", job.Name);

        if (!Directory.Exists(_options.InputDirectory))
        {
            _logger.LogWarning("Input directory missing: {Dir}", _options.InputDirectory);
            return;
        }

        var files = Directory.GetFiles(_options.InputDirectory, job.FileGlob);

        if (files.Length == 0)
        {
            _logger.LogWarning("No files matched glob {Glob}", job.FileGlob);
            return;
        }

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            await ProcessFileAsync(job, file, ct);
        }

        _logger.LogInformation("Job finished: {Job}", job.Name);
    }

    private async Task ProcessFileAsync(ImportJob job, string filePath, CancellationToken ct)
    {
        _logger.LogInformation("Processing file: {File}", filePath);

        var batch = new List<string>(_options.ChunkSize);

        try
        {
            await using var stream = File.OpenRead(filePath);
            using var reader = new StreamReader(stream);

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync(ct);
                if (line is null)
                    break;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                batch.Add(line);

                if (batch.Count >= _options.ChunkSize)
                {
                    await ProcessBatchAsync(job, batch, ct);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await ProcessBatchAsync(job, batch, ct);
                batch.Clear();
            }

            _logger.LogInformation("Finished file: {File}", filePath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Import cancelled while reading {File}", filePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing file {File}", filePath);
        }
    }

    private async Task ProcessBatchAsync(
        ImportJob job,
        List<string> batch,
        CancellationToken ct)
    {
        try
        {
            // Placeholder for actual parsing + DB import
            // Hook your parser + EF bulk insert here

            _logger.LogInformation(
                "Processing batch of {Count} records for job {Job}",
                batch.Count,
                job.Name);

            await Task.Yield(); // keep async pipeline responsive
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Batch processing failed for job {Job}",
                job.Name);
        }
    }
}
