#nullable enable
using System.Collections;
using System.Reflection;
using CobolFixedWidthImport.Data;
using CobolFixedWidthImport.Parsing.Config;
using CobolFixedWidthImport.Parsing.FixedWidth;
using CobolFixedWidthImport.Parsing.Mapping;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CobolFixedWidthImport.Import;

public sealed class FlatFileImportHostedService(
    ILogger<FlatFileImportHostedService> logger,
    IServiceScopeFactory scopeFactory,
    ImportManifestLoader manifestLoader,
    LayoutConfigLoader layoutLoader,
    FixedWidthRecordParser recordParser,
    EntityTypeRegistry entityRegistry,
    PropertySetterCache setterCache,
    IOptions<ImportOptions> options)
    : BackgroundService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ImportManifestLoader _manifestLoader = manifestLoader;
    private readonly LayoutConfigLoader _layoutLoader = layoutLoader;
    private readonly FixedWidthRecordParser _recordParser = recordParser;
    private readonly EntityTypeRegistry _entityRegistry = entityRegistry;
    private readonly PropertySetterCache _setterCache = setterCache;
    private readonly ImportOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("InputDirectory: {Dir}", _options.InputDirectory);
        _logger.LogInformation("ManifestPath: {Manifest}", _options.ManifestPath);
        _logger.LogInformation("ChunkSize: {Chunk}", _options.ChunkSize);

        var manifest = await _manifestLoader.LoadAsync(_options.ManifestPath, ct);

        foreach (var job in manifest.Jobs)
        {
            ct.ThrowIfCancellationRequested();
            await RunJobAsync(job, ct);
        }

        _logger.LogInformation("All import jobs complete.");
    }

    private async Task RunJobAsync(ImportJob job, CancellationToken ct)
    {
        _logger.LogInformation("Job start: {Job} (Mode={Mode}, Entity={Entity}, Glob={Glob}, Layout={Layout})",
            job.Name, job.Mode, job.TargetEntity, job.FileGlob, job.LayoutPath);

        var inputDir = ResolvePath(_options.InputDirectory);
        if (!Directory.Exists(inputDir))
        {
            _logger.LogWarning("Input directory does not exist: {Dir}", inputDir);
            return;
        }

        var layoutPath = ResolvePath(job.LayoutPath);
        var layout = await _layoutLoader.LoadAsync(layoutPath, ct);

        var targetType = _entityRegistry.Resolve(job.TargetEntity);
        var mode = (job.Mode ?? "").Trim().ToLowerInvariant();

        var files = Directory.GetFiles(inputDir, job.FileGlob, SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            _logger.LogWarning("No files matched. Dir={Dir} Glob={Glob}", inputDir, job.FileGlob);
            return;
        }

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            await ImportFileAsync(job, file, layout, targetType, mode, ct);
        }

        _logger.LogInformation("Job done: {Job}", job.Name);
    }

    private async Task ImportFileAsync(
        ImportJob job,
        string filePath,
        LayoutConfig.Layout layout,
        Type targetType,
        string mode,
        CancellationToken ct)
    {
        _logger.LogInformation("Importing file: {File}", filePath);

        // Keep your existing ImportContext usage (parsers may rely on it).
        // This context is ONLY metadata for parsing/import; it doesn't require ImportJob extras.
        var ctx = new ImportContext
        {
            ImportBatchId = Guid.NewGuid().ToString("N"),
            ImportedAtUtc = DateTime.UtcNow,
            SourceFile = Path.GetFileName(filePath),
            JobName = job.Name
        };

        var buffer = new List<object>(_options.ChunkSize);
        long totalRead = 0;
        long totalParsed = 0;
        long totalInserted = 0;
        int lineNumber = 0;

        await using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct);
            if (line is null)
                break;

            lineNumber++;
            totalRead++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                object entity = mode switch
                {
                    "single" => _recordParser.ParseSingle(line, targetType, layout, ctx),
                    "graph" => _recordParser.ParseGraph(line, targetType, layout, ctx),
                    _ => throw new InvalidOperationException(
                        $"Job '{job.Name}': unsupported mode '{job.Mode}'. Expected 'single' or 'graph'.")
                };

                // Stamp import metadata onto the entity IF those properties exist.
                ApplyImportMetadata(entity, ctx);

                buffer.Add(entity);
                totalParsed++;

                if (buffer.Count >= _options.ChunkSize)
                {
                    var inserted = await BulkInsertChunkAsync(mode, targetType, buffer, ct);
                    totalInserted += inserted;
                    buffer.Clear();

                    _logger.LogInformation("Progress {Job}: {Inserted} inserted so far (File={File})",
                        job.Name, totalInserted, Path.GetFileName(filePath));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Parse error. Job={Job} File={File} Line={Line}. RawLine={Raw}",
                    job.Name, Path.GetFileName(filePath), lineNumber, line);
            }
        }

        if (buffer.Count > 0)
        {
            var inserted = await BulkInsertChunkAsync(mode, targetType, buffer, ct);
            totalInserted += inserted;
            buffer.Clear();
        }

        _logger.LogInformation(
            "File done: {File}. Read={Read} Parsed={Parsed} Inserted={Inserted}",
            Path.GetFileName(filePath), totalRead, totalParsed, totalInserted);
    }

    /// <summary>
    /// Stamps common import metadata columns if they exist on the entity type.
    /// Does NOT require any extra YAML options on ImportJob.
    /// </summary>
    private void ApplyImportMetadata(object entity, ImportContext ctx)
    {
        TrySet(entity, "ImportedAtUtc", ctx.ImportedAtUtc);
        TrySet(entity, "ImportBatchId", ctx.ImportBatchId);
        TrySet(entity, "SourceFile", ctx.SourceFile);
        TrySet(entity, "JobName", ctx.JobName);
    }

    private void TrySet(object entity, string propertyName, object? value)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return;

        // Uses your existing setter cache (fast) if property exists.
        var setter = _setterCache.TryGet(entity.GetType(), propertyName);
        if (setter is null)
            return;

        try { setter(entity, value); }
        catch
        {
            // Ignore conversion issues; keep import resilient.
        }
    }

    private async Task<int> BulkInsertChunkAsync(
        string mode,
        Type entityType,
        List<object> buffer,
        CancellationToken ct)
    {
        var typedList = CreateTypedList(entityType, buffer);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var bulkConfig = new BulkConfig
        {
            BatchSize = buffer.Count,
            SetOutputIdentity = true,
            IncludeGraph = mode == "graph"
        };

        await InvokeBulkInsertAsync(db, entityType, typedList, bulkConfig, ct);

        await tx.CommitAsync(ct);
        return buffer.Count;
    }

    private static IList CreateTypedList(Type elementType, IEnumerable<object> items)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;

        foreach (var obj in items)
            list.Add(obj);

        return list;
    }

    private static Task InvokeBulkInsertAsync(
        DbContext db,
        Type entityType,
        IList typedList,
        BulkConfig bulkConfig,
        CancellationToken ct)
    {
        // Find EFCore.BulkExtensions extension method:
        // BulkInsertAsync<T>(DbContext, IList<T>, BulkConfig, Action<decimal>?, CancellationToken)
        var methods = typeof(DbContextBulkExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static);

        var method = methods
            .Where(m => m.Name == nameof(DbContextBulkExtensions.BulkInsertAsync))
            .Where(m => m.IsGenericMethodDefinition)
            .Select(m => new { Method = m, Params = m.GetParameters() })
            .Where(x =>
                x.Params.Length == 5 &&
                x.Params[0].ParameterType == typeof(DbContext) &&
                x.Params[2].ParameterType == typeof(BulkConfig) &&
                x.Params[3].ParameterType == typeof(Action<decimal>) &&
                x.Params[4].ParameterType == typeof(CancellationToken))
            .Select(x => x.Method)
            .FirstOrDefault();

        if (method is null)
            throw new MissingMethodException(
                "EFCore.BulkExtensions BulkInsertAsync<T>(DbContext, IList<T>, BulkConfig, Action<decimal>, CancellationToken) not found.");

        var generic = method.MakeGenericMethod(entityType);

        var taskObj = generic.Invoke(null, new object?[] { db, typedList, bulkConfig, null, ct });

        return taskObj as Task
               ?? throw new InvalidOperationException("BulkInsertAsync invocation did not return a Task.");
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }
}
