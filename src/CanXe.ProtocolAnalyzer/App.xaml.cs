using System.IO;
using System.Windows;
using CanXe.ProtocolAnalyzer.Core.Analysis;
using CanXe.ProtocolAnalyzer.Core.Reporting;
using CanXe.ProtocolAnalyzer.ViewModels;

namespace CanXe.ProtocolAnalyzer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (TryRunBatchAnalysis(e.Args))
            return;

        base.OnStartup(e);
        var vm = new ProtocolAnalyzerViewModel();
        var window = new MainWindow { DataContext = vm };
        MainWindow = window;
        window.Show();
    }

    private static bool TryRunBatchAnalysis(string[] args)
    {
        if (args.Length == 0 || !args.Contains("--analyze-batch", StringComparer.OrdinalIgnoreCase))
            return false;

        var inputDir = GetArg(args, "--input") ?? @"C:\CanXeProtocolSamples";
        var outputDir = GetArg(args, "--output") ?? inputDir;
        var files = Directory.Exists(inputDir)
            ? Directory.GetFiles(inputDir, "CanXe_COM1_*.log").OrderBy(f => f).ToList()
            : [];

        if (files.Count == 0)
        {
            MessageBox.Show($"Không tìm thấy log trong {inputDir}", "Protocol Analyzer");
            Current?.Shutdown(-1);
            return true;
        }

        var orchestrator = new ProtocolAnalysisOrchestrator();
        var result = orchestrator.Analyze(files);
        var writer = new ProtocolAnalysisReportWriter();
        var mdPath = Path.Combine(outputDir, "ProtocolAnalysisReport.md");
        var jsonPath = Path.Combine(outputDir, "ProtocolAnalysisReport.json");
        writer.WriteMarkdownAsync(result, mdPath).GetAwaiter().GetResult();
        writer.WriteJsonAsync(result, jsonPath).GetAwaiter().GetResult();
        Current?.Shutdown(0);
        return true;
    }

    private static string? GetArg(string[] args, string key)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }
}
