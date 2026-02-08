using CobolFixedWidthImport.Data;
using CobolFixedWidthImport.Parsing.Config;
using CobolFixedWidthImport.Parsing.FixedWidth;
using CobolFixedWidthImport.Parsing.Mapping;
using EFCore.BulkExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CobolFixedWidthImport.Import;

public sealed class FlatFileImportHostedService(
    ILogger<FlatFileImportHostedService> logger,
    IOptions<ImportOptions> options,
    ImportManifestLoader manifestLoader,
    LayoutConfigLoader layoutLoader,
    EntityTypeRegistry typeRegistry,
    FixedWidthRecordParser recordParser,
    IServiceProvider services)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opt = options.Value;
        var inputDir = Path.GetFullPath(opt.InputDirectory);
        Directory.CreateDirectory(inputDir);

        logger.LogInformation("InputDirectory: {InputDir}", inputDir);
        logger.LogInformation("ManifestPath: {ManifestPath}", opt.ManifestPath);
        logger.LogInformation("ChunkSize: {ChunkSize}", opt.ChunkSize);

        var manifest = await manifestLoader.LoadAsync(opt.ManifestPath, stoppingToken);
        if (manifest.Imports.Count == 0)
        {
            logger.LogWarning("Manifest contains no imports.");
            return;
        }

        foreach (var job in manifest.Imports)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var files = Directory.EnumerateFiles(inputDir, job.InputPattern, SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
            {
                logger.LogWarning("Job {Job}: no files match pattern {Pattern} in {Dir}", job.Name, job.InputPattern, inputDir);
                continue;
            }

            var layout = await layoutLoader.LoadAsync(job.LayoutFile, stoppingToken);

            // Shared timestamp per job (your requirement)
            var ctx = new ImportContext(
                ImportedAtUtc: DateTime.UtcNow,
                SourceSystem: job.SourceSystem ?? "UNKNOWN",
                BatchId: job.BatchId ?? $"{job.Name}-{DateTime.UtcNow:yyyyMMddHHmmss}");

            logger.LogInformation("Job {Job} started. Mode={Mode}. Layout={LayoutFile}. Files={Count}. ImportedAtUtc={ImportedAtUtc}",
                job.Name, job.Mode, job.LayoutFile, files.Count, ctx.ImportedAtUtc);

            foreach (var file in files)
            {
                stoppingToken.ThrowIfCancellationRequested();
                await ImportFileAsync(job, file, layout, ctx, opt.ChunkSize, stoppingToken);
            }

            logger.LogInformation("Job {Job} finished.", job.Name);
        }

        logger.LogInformation("All jobs completed.");
    }

    private async Task ImportFileAsync(
        ImportManifest.ImportJob job,
        string filePath,
        LayoutConfig.Layout layout,
        ImportContext ctx,
        int chunkSize,
        CancellationToken ct)
    {
        var fileName = Path.GetFileName(filePath);
        logger.LogInformation("Processing file: {File} (Job={Job})", fileName, job.Name);

        using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);

        long lineNumber = 0;

        if (job.Mode.Equals("single", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(job.Entity))
                throw new InvalidOperationException($"Job {job.Name} mode=single requires entity.");

            var entityType = typeRegistry.Resolve(job.Entity);
            var buffer = new List<object>(chunkSize);

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync(ct);
                if (line is null)
                    break;

                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var entity = recordParser.ParseSingle(line, entityType, layout, ctx);
                    buffer.Add(entity);

                    if (buffer.Count >= chunkSize)
                    {
                        await BulkInsertSingleAsync(entityType, buffer, ct);
                        buffer.Clear();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Parse error. Job={Job} File={File} Line={Line}. RawLine={Raw}",
                        job.Name, fileName, lineNumber, line);
                }
            }


            if (buffer.Count > 0)
                await BulkInsertSingleAsync(entityType, buffer, ct);
        }
        else if (job.Mode.Equals("graph", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(job.ParentEntity))
                throw new InvalidOperationException($"Job {job.Name} mode=graph requires parentEntity.");

            var parentType = typeRegistry.Resolve(job.ParentEntity);
            var buffer = new List<object>(chunkSize);

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync(ct);
                if (line is null)
                    break;

                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var parent = recordParser.ParseGraph(line, parentType, layout, ctx);
                    buffer.Add(parent);

                    if (buffer.Count >= chunkSize)
                    {
                        await BulkInsertGraphAsync(buffer, ct);
                        buffer.Clear();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Parse error. Job={Job} File={File} Line={Line}. RawLine={Raw}",
                        job.Name, fileName, lineNumber, line);
                }
            }


            if (buffer.Count > 0)
                await BulkInsertGraphAsync(buffer, ct);
        }
        else
        {
            throw new InvalidOperationException($"Unknown job mode '{job.Mode}' for job {job.Name}.");
        }

        logger.LogInformation("Finished file: {File} (Job={Job})", fileName, job.Name);
    }

    private async Task BulkInsertSingleAsync(Type entityType, List<object> entities, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.BulkInsertAsync(
                entities,
                new BulkConfig
                {
                    SetOutputIdentity = true,
                    BatchSize = entities.Count
                },
                progress: null,
                cancellationToken: ct);

            await tx.CommitAsync(ct);
            logger.LogInformation("Inserted single chunk: Type={Type}, Count={Count}", entityType.Name, entities.Count);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task BulkInsertGraphAsync(List<object> parents, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.BulkInsertAsync(
                parents,
                new BulkConfig
                {
                    IncludeGraph = true,
                    SetOutputIdentity = true,
                    BatchSize = parents.Count
                },
                progress: null,
                cancellationToken: ct);


            await tx.CommitAsync(ct);
            logger.LogInformation("Inserted graph chunk: ParentCount={Count}", parents.Count);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
