using System.IO;
using System.Text.Json;

using CanXe.Application.Configuration;

using CanXe.Application.Interfaces;

using CanXe.Application.Models;

using CanXe.Infrastructure;

using CanXe.Desktop.Services;
using CanXe.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Hosting;

using Microsoft.Extensions.Logging;



namespace CanXe.Diagnostics;



internal static class Program

{

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length >= 2 && string.Equals(args[0], "--export-catalog-seed", StringComparison.OrdinalIgnoreCase))
            {
                var liveDb = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanXe", "canxe.db");
                var seed = await CanXe.Infrastructure.Data.CatalogSeedExporter.ExportAsync(liveDb, args[1]);
                Console.WriteLine(
                    $"Seed written: {args[1]} (customers={seed.Customers}, vehicles={seed.Vehicles}, cargoTypes={seed.CargoTypes})");
                return 0;
            }

            if (args.Length >= 2 && string.Equals(args[0], "--report-print-test", StringComparison.OrdinalIgnoreCase))
            {
                var rows = args.Length >= 3 && int.TryParse(args[2], out var n) ? n : 40;
                return CanXe.Desktop.Services.ReportPrintTestRunner.Run(
                    args[1], rows,
                    portrait: args.Contains("--portrait", StringComparer.OrdinalIgnoreCase),
                    multiCustomer: args.Contains("--multi", StringComparer.OrdinalIgnoreCase));
            }

            if (args.Length >= 2 && string.Equals(args[0], "--ticket-price-split-print-test", StringComparison.OrdinalIgnoreCase))
                return CanXe.Desktop.Services.TicketPriceSplitPrintTestRunner.Run(args[1]);

            var cli = DiagnosticsArgumentParser.Parse(args);

            if (cli.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            if (cli.HasScaleFlagConflict)
            {
                Console.Error.WriteLine("ERROR: Conflicting flags — use either --require-scale or --skip-scale, not both.");
                return 2;
            }



            var settings = LoadSettings();

            if (cli.PublishCheck && !CanXe.Domain.Services.ProductionConfigPolicy.IsHardwareMode(settings.DeviceMode))

            {

                Console.Error.WriteLine($"ERROR: DeviceMode must be Hardware for publish-check (current: {settings.DeviceMode})");

                return 2;

            }



            var appData = Path.Combine(

                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),

                "CanXe");

            Directory.CreateDirectory(appData);

            var dbPath = Path.Combine(appData, "canxe.db");

            var photoRoot = Path.Combine(appData, "Photos");

            Directory.CreateDirectory(photoRoot);



            var host = Host.CreateDefaultBuilder(args)

                .ConfigureLogging(logging =>

                {

                    if (!cli.Verbose)

                    {

                        logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

                        logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Warning);

                        logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

                    }

                })

                .ConfigureServices(services =>

                {

                    services.AddCanXeInfrastructure(settings, dbPath, photoRoot);

                    services.AddSingleton(settings);

                })

                .Build();



            await host.StartAsync();

            await DependencyInjection.InitializeDatabaseAsync(host.Services, dbPath);

            if (cli.ScaleLiveSeconds is int scaleLiveSeconds && scaleLiveSeconds > 0)
            {
                var exit = await ScaleLiveRunner.RunAsync(
                    host.Services,
                    scaleLiveSeconds,
                    cli.VerboseScaleFrames).ConfigureAwait(false);
                await host.StopAsync();
                return exit;
            }

            if (cli.PrintLayoutGeometryTest)
            {
                var exit = PrintLayoutGeometryTestRunner.Run();
                await host.StopAsync();
                return exit;
            }

            if (cli.PrintA4TwoUpTest)
            {
                var exit = PrintA4TwoUpTestRunner.Run(cli.PrintRenderSendToPrinter);
                await host.StopAsync();
                return exit;
            }

            if (cli.PrintA5FitTest)
            {
                var exit = PrintA5FitTestRunner.Run(cli.PrintRenderSendToPrinter);
                await host.StopAsync();
                return exit;
            }

            if (cli.PrintRenderTest)
            {
                var exit = PrintRenderTestRunner.Run(cli.PrintRenderSendToPrinter);
                await host.StopAsync();
                return exit;
            }

            if (cli.TakeWeightPerformanceCycles is int performanceCycles && performanceCycles > 0)
            {
                var exit = await TakeWeightPerformanceRunner.RunAsync(
                    host.Services,
                    performanceCycles).ConfigureAwait(false);
                await host.StopAsync();
                return exit;
            }

            var diagnostics = host.Services.GetRequiredService<ISystemDiagnosticsService>();

            var runOptions = DiagnosticsArgumentParser.BuildRunOptions(cli);

            SystemDiagnosticsResult? result = null;
            var exitCode = 1;

            try
            {
                using var overallCts = new CancellationTokenSource(DiagnosticsTimeouts.Overall);
                result = await diagnostics.RunAllAsync(runOptions, overallCts.Token).ConfigureAwait(false);
                PrintResults(result, cli.JsonOutput);
                exitCode = DiagnosticsExitEvaluator.Evaluate(result, runOptions).ExitCode;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine($"ERROR: Diagnostics timed out after {DiagnosticsTimeouts.Overall.TotalSeconds:F0}s");
                result ??= new SystemDiagnosticsResult();
                exitCode = 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                ReleaseVerificationLogger.Write($"Diagnostics error: {ex.Message}");
                result ??= new SystemDiagnosticsResult();
                exitCode = 1;
            }
            finally
            {
                DiagnosticsProgressReporter.RunStarted("Export report");
                try
                {
                    var reportPath = await diagnostics.ExportReportAsync(result ?? new SystemDiagnosticsResult())
                        .ConfigureAwait(false);
                    Console.WriteLine($"Report: {reportPath}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ERROR: Failed to export report: {ex.Message}");
                }

                var summary = DiagnosticsExitEvaluator.Evaluate(result ?? new SystemDiagnosticsResult(), runOptions);
                Console.WriteLine($"Required scale: {summary.RequiredScale}");
                Console.WriteLine($"Scale result: {summary.ScaleResult}");
                Console.WriteLine($"Final exit code: {summary.ExitCode}");

                if (summary.ExitCode == 0 && result is not null)
                    Console.WriteLine($"Result: PASS ({result.Passed} passed, {result.Failed} failed)");

                exitCode = summary.ExitCode;
                DiagnosticsProgressReporter.RunPassed("Export report");
                await host.StopAsync().ConfigureAwait(false);
            }

            return exitCode;

        }

        catch (Exception ex)

        {

            Console.Error.WriteLine($"ERROR: {ex.Message}");

            ReleaseVerificationLogger.Write($"CLI error: {ex.Message}");

            return 1;

        }

    }



    private static string? ResolveTestMediaPath()

    {

        var candidates = new[]

        {

            Path.Combine(AppContext.BaseDirectory, "TestAssets", "camera-loop.mp4"),

            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tests", "TestAssets", "camera-loop.mp4"),

            Path.Combine(Directory.GetCurrentDirectory(), "tests", "TestAssets", "camera-loop.mp4")

        };



        foreach (var path in candidates)

        {

            var full = Path.GetFullPath(path);

            if (File.Exists(full))

                return full;

        }



        return null;

    }



    private static void PrintResults(SystemDiagnosticsResult result, bool json)

    {

        if (json)

        {

            var payload = result.Checks.Select(c => new

            {

                c.Category,

                c.Name,

                Status = c.Status.ToString().ToUpperInvariant(),

                c.Detail,

                DurationMs = c.Duration.TotalMilliseconds

            });

            Console.WriteLine(JsonSerializer.Serialize(new

            {

                passed = result.Passed,

                failed = result.Failed,

                warnings = result.Warnings,

                skipped = result.Skipped,

                checks = payload

            }, new JsonSerializerOptions { WriteIndented = true }));

            return;

        }



        foreach (var check in result.Checks)

        {

            var tag = check.Status switch

            {

                DiagnosticStatus.Pass => "PASS",

                DiagnosticStatus.Fail => "FAIL",

                DiagnosticStatus.Warning => "WARN",

                DiagnosticStatus.Skipped => "SKIP",

                _ => "????"

            };

            Console.WriteLine($"[{tag}] {check.Category}/{check.Name}: {RtspCredentialSanitizer.Redact(check.Detail)}");

        }

    }



    private static AppSettings LoadSettings()

    {

        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(path))

            return new AppSettings();



        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions

        {

            PropertyNameCaseInsensitive = true

        }) ?? new AppSettings();

    }



    private static void PrintHelp()

    {

        Console.WriteLine("""

            CanXe.Diagnostics — automated acceptance checks



            Usage:

              CanXe.Diagnostics.exe --all

              CanXe.Diagnostics.exe --all --require-camera --require-scale

              CanXe.Diagnostics.exe --camera

              CanXe.Diagnostics.exe --scale

              CanXe.Diagnostics.exe --database

              CanXe.Diagnostics.exe --publish-check

              CanXe.Diagnostics.exe --json

              CanXe.Diagnostics.exe --verbose



            Options:

              --require-camera / --skip-camera

              --require-scale / --skip-scale
              --capture-camera-pipe
              --parse-camera-capture <file.bin>
              --camera-stability <seconds>
              --scale-live <seconds>
              --take-weight-performance <cycles>
              --verbose-scale-frames
              --export-catalog-seed <out.db>   (catalogs only, no tickets/settings)

            Exit codes:

              0 = all required checks pass

              1 = failure

              2 = invalid config / conflicting flags

              3 = dependency missing

            """);

    }

}


