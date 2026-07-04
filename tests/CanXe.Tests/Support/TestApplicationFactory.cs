using CanXe.Application.Configuration;
using CanXe.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Support;

public sealed class TestApplicationFactory : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly string _photoRoot;
    private readonly bool _deleteOnDispose;

    public TestApplicationFactory(
        string? databasePath = null,
        string? photoRootPath = null,
        bool deleteOnDispose = true)
    {
        _deleteOnDispose = deleteOnDispose;
        _dbPath = databasePath ?? Path.Combine(Path.GetTempPath(), $"canxe-test-{Guid.NewGuid():N}.db");
        _photoRoot = photoRootPath ?? Path.Combine(Path.GetTempPath(), $"canxe-photos-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_photoRoot);

        var settings = new AppSettings();
        var services = new ServiceCollection();
        services.AddCanXeInfrastructure(settings, _dbPath, _photoRoot);
        Provider = services.BuildServiceProvider();
    }

    public string DatabasePath => _dbPath;

    public string PhotoRoot => _photoRoot;

    public ServiceProvider Provider { get; }

    public async Task InitializeAsync() =>
        await DependencyInjection.InitializeDatabaseAsync(Provider, _dbPath);

    public async ValueTask DisposeAsync()
    {
        await Provider.DisposeAsync();
        if (_deleteOnDispose)
        {
            TryDelete(_dbPath);
            TryDeleteDirectory(_photoRoot);
        }
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
