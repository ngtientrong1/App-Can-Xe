using System.Text;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Infrastructure.Diagnostics;

public sealed class SystemDiagnosticsService(IServiceScopeFactory scopeFactory) : ISystemDiagnosticsService
{
    public Task<SystemDiagnosticsResult> RunAllAsync(
        DiagnosticsRunOptions options,
        CancellationToken cancellationToken = default) =>
        new DiagnosticsRunOrchestrator(scopeFactory).RunAsync(options, cancellationToken);

    public async Task<string> ExportReportAsync(
        SystemDiagnosticsResult result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe",
            "Diagnostics");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

        var sb = new StringBuilder();
        sb.AppendLine("CanXe System Diagnostics Report");
        sb.AppendLine($"Generated: {DateTimeOffset.Now:O}");
        sb.AppendLine($"Duration: {result.TotalDuration.TotalSeconds:F2}s");
        sb.AppendLine(new string('=', 72));
        sb.AppendLine($"{"Category",-16} {"Check",-32} {"Status",-8} {"Duration",8} Detail");
        sb.AppendLine(new string('-', 72));

        foreach (var check in result.Checks)
        {
            sb.AppendLine(
                $"{check.Category,-16} {check.Name,-32} {check.Status,-8} {check.Duration.TotalMilliseconds,6:F0}ms {RtspCredentialSanitizer.Redact(check.Detail)}");
        }

        sb.AppendLine(new string('=', 72));
        sb.AppendLine($"Summary: {result.Passed} passed, {result.Failed} failed, {result.Warnings} warnings, {result.Skipped} skipped");

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        DiagnosticsLogger.Write($"Report exported: {path}");
        return path;
    }
}
