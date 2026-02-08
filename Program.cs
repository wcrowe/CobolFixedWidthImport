using CobolFixedWidthImport.Data;
using CobolFixedWidthImport.Import;
using CobolFixedWidthImport.Parsing.Config;
using CobolFixedWidthImport.Parsing.FixedWidth;
using CobolFixedWidthImport.Parsing.Mapping;
using CobolFixedWidthImport.Parsing.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CobolFixedWidthImport;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Do NOT wrap CTS in a using. We dispose it after host shutdown.
        var cts = new CancellationTokenSource();

        ConsoleCancelEventHandler? handler = (_, e) =>
        {
            // Prevent abrupt termination; request graceful shutdown.
            e.Cancel = true;

            if (!cts.IsCancellationRequested)
                cts.Cancel();
        };

        Console.CancelKeyPress += handler;

        try
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Configuration
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            builder.Services.AddOptions<ImportOptions>()
                .Bind(builder.Configuration.GetSection("Import"))
                .ValidateDataAnnotations()
                .Validate(o => o.ChunkSize is > 0 and <= 100_000, "ChunkSize must be between 1 and 100000.")
                .ValidateOnStart();

            var connStr = builder.Configuration.GetConnectionString("SqlServer");
            if (string.IsNullOrWhiteSpace(connStr))
                throw new InvalidOperationException("Missing connection string: ConnectionStrings:SqlServer");

            builder.Services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseSqlServer(connStr, sql => sql.EnableRetryOnFailure(5));
            });

            // YAML loaders
            builder.Services.AddSingleton<ImportManifestLoader>();
            builder.Services.AddSingleton<LayoutConfigLoader>();

            // Parsing core
            builder.Services.AddSingleton<ParserFactory>();
            builder.Services.AddSingleton<ValueSourceResolver>();
            builder.Services.AddSingleton<PropertySetterCache>();
            builder.Services.AddSingleton<CollectionAdderCache>();
            builder.Services.AddSingleton<EntityTypeRegistry>();
            builder.Services.AddSingleton<FixedWidthRecordParser>();

            // Hosted import service
            builder.Services.AddHostedService<FlatFileImportHostedService>();

            var host = builder.Build();

            await EnsureDatabaseAsync(host.Services, cts.Token);

            await host.RunAsync(cts.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            // Unhook event to avoid handler firing after dispose.
            if (handler is not null)
                Console.CancelKeyPress -= handler;

            cts.Dispose();
        }
    }

    private static async Task EnsureDatabaseAsync(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Demo convenience. In production, prefer migrations.
        await db.Database.EnsureCreatedAsync(ct);
    }
}
