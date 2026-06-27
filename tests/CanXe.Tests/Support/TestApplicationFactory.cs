using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Infrastructure;
using CanXe.Infrastructure.Camera;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Support;

public sealed class TestApplicationFactory : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly string _photoRoot;

    public TestApplicationFactory(bool simulateCameraFailure = false)
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"canxe-test-{Guid.NewGuid():N}.db");
        _photoRoot = Path.Combine(Path.GetTempPath(), $"canxe-photos-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_photoRoot);

        var settings = new AppSettings { SimulateCameraFailure = simulateCameraFailure };
        var services = new ServiceCollection();
        services.AddCanXeInfrastructure(settings, _dbPath, _photoRoot);
        Provider = services.BuildServiceProvider();
    }

    public ServiceProvider Provider { get; }

    public async Task InitializeAsync() =>
        await DependencyInjection.InitializeDatabaseAsync(Provider, _dbPath);

    public async ValueTask DisposeAsync()
    {
        await Provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        TryDelete(_dbPath);
        TryDeleteDirectory(_photoRoot);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}

public sealed class FailingThenSucceedingCameraService : ICameraService
{
    private int _calls;

    public int CallCount => _calls;

    public Task<CameraCaptureResult> CaptureAsync(
        PhotoCaptureRequest request,
        string targetFilePath,
        CancellationToken cancellationToken = default)
    {
        _calls++;
        if (_calls == 1)
            return Task.FromResult(CameraCaptureResult.Failed("Simulated failure"));

        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
        File.WriteAllBytes(targetFilePath, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        return Task.FromResult(CameraCaptureResult.Succeeded(targetFilePath));
    }
}

public sealed class SucceedThenFailCameraService : ICameraService
{
    private int _calls;

    public Task<CameraCaptureResult> CaptureAsync(
        PhotoCaptureRequest request,
        string targetFilePath,
        CancellationToken cancellationToken = default)
    {
        _calls++;
        if (_calls >= 2)
            return Task.FromResult(CameraCaptureResult.Failed("Simulated failure on update"));

        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
        File.WriteAllBytes(targetFilePath, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        return Task.FromResult(CameraCaptureResult.Succeeded(targetFilePath));
    }
}
