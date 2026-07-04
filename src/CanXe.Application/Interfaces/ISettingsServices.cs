using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface IStationSettingsRepository
{
    Task<StationSettingsDto?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(StationSettingsDto settings, CancellationToken cancellationToken = default);
}

public interface IScaleDeviceSettingsRepository
{
    Task<ScaleDeviceSettingsDto?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ScaleDeviceSettingsDto settings, CancellationToken cancellationToken = default);
}

public interface ICameraDeviceSettingsRepository
{
    Task<CameraDeviceSettingsDto?> GetAsync(CancellationToken cancellationToken = default);
    Task<CameraRuntimeSettings?> GetRuntimeAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CameraDeviceSettingsDto settings, CancellationToken cancellationToken = default);
}

public interface ISecretProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}

public interface ICameraConnectionTester
{
    Task<ConnectionTestResult> TestAsync(CameraDeviceSettingsDto settings, CancellationToken cancellationToken = default);
}

public interface IScaleConnectionTester
{
    Task<ConnectionTestResult> TestAsync(ScaleDeviceSettingsDto settings, CancellationToken cancellationToken = default);
}

public interface IPrintSettingsRepository
{
    Task<PrintSettingsDto?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PrintSettingsDto settings, CancellationToken cancellationToken = default);
}

public interface IPrintJobHistoryRepository
{
    Task<long> AddAsync(PrintJobHistoryDto job, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(long id, string status, string? errorMessage, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrintJobHistoryDto>> GetForTicketAsync(int ticketId, CancellationToken cancellationToken = default);
}
