using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface ISystemDiagnosticsService
{
    Task<SystemDiagnosticsResult> RunAllAsync(DiagnosticsRunOptions options, CancellationToken cancellationToken = default);
    Task<string> ExportReportAsync(SystemDiagnosticsResult result, CancellationToken cancellationToken = default);
}
