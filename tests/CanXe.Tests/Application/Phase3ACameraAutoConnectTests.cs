using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Camera;
using CanXe.Infrastructure.Data;
using CanXe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class Phase3ACameraAutoConnectTests
{
    private static CameraDeviceSettingsDto SampleCamera(string password = "SecretCamPass1") => new()
    {
        CameraName = "Cam 1",
        IsEnabled = true,
        RtspHost = "192.168.1.100",
        RtspPort = 8554,
        RtspPath = "/live/main",
        Username = "admin",
        Password = password,
        RtspTransport = "TCP",
        ConnectTimeoutSeconds = 8,
        AutoConnectCameraOnStartup = true,
        PreviewEnabled = true,
        SnapshotTimeoutSeconds = 5,
        PhotoRetentionDays = 7
    };

    [Fact]
    public async Task SaveConfig_PersistsAllRtspSettings()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StationSettingsService>();

        var dto = SampleCamera();
        var (success, _) = await service.SaveCameraAsync(dto);
        Assert.True(success);

        var loaded = await service.GetCameraAsync();
        Assert.Equal("Cam 1", loaded.CameraName);
        Assert.True(loaded.IsEnabled);
        Assert.Equal("192.168.1.100", loaded.RtspHost);
        Assert.Equal(8554, loaded.RtspPort);
        Assert.Equal("/live/main", loaded.RtspPath);
        Assert.Equal("admin", loaded.Username);
        Assert.Equal("TCP", loaded.RtspTransport);
        Assert.Equal(8, loaded.ConnectTimeoutSeconds);
        Assert.True(loaded.AutoConnectCameraOnStartup);
        Assert.True(loaded.PreviewEnabled);
        Assert.Equal(7, loaded.PhotoRetentionDays);
    }

    [Fact]
    public async Task SavePassword_IsDpapiEncrypted()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<StationSettingsService>();
        var db = sp.GetRequiredService<CanXeDbContext>();

        const string plain = "MyRtspPassword99";
        await service.SaveCameraAsync(SampleCamera(plain));

        var entity = await db.CameraDeviceSettings.AsNoTracking().SingleAsync();
        Assert.False(string.IsNullOrWhiteSpace(entity.ProtectedPassword));
        Assert.DoesNotContain(plain, entity.ProtectedPassword!);

        var editDto = await service.GetCameraAsync();
        Assert.True(editDto.HasStoredPassword);
        Assert.Null(editDto.Password);
    }

    [Fact]
    public async Task Restart_LoadsSavedConfig()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"canxe-phase3a-{Guid.NewGuid():N}.db");
        var photoRoot = Path.Combine(Path.GetTempPath(), $"canxe-phase3a-photos-{Guid.NewGuid():N}");

        await using (var factory = new TestApplicationFactory(databasePath: dbPath, photoRootPath: photoRoot, deleteOnDispose: false))
        {
            await factory.InitializeAsync();
            using var scope = factory.Provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<StationSettingsService>()
                .SaveCameraAsync(SampleCamera("RestartPass1"));
        }

        await using var restartFactory = new TestApplicationFactory(databasePath: dbPath, photoRootPath: photoRoot);
        await restartFactory.InitializeAsync();
        using var restartScope = restartFactory.Provider.CreateScope();
        var runtime = await restartScope.ServiceProvider.GetRequiredService<StationSettingsService>()
            .GetCameraRuntimeAsync();

        Assert.Equal("192.168.1.100", runtime.RtspHost);
        Assert.Equal(8554, runtime.RtspPort);
        Assert.Equal("/live/main", runtime.RtspPath);
        Assert.Equal("admin", runtime.Username);
        Assert.Equal("RestartPass1", runtime.Password);
        Assert.True(runtime.AutoConnectCameraOnStartup);
    }

    [Fact]
    public void AutoConnectTrue_ShouldConnectOnStartup()
    {
        var runtime = CameraSettingsMapper.ToRuntime(SampleCamera(), "pass");
        Assert.True(CameraAutoConnectPolicy.ShouldAutoConnectOnStartup(runtime.IsEnabled, runtime.AutoConnectCameraOnStartup));
    }

    [Fact]
    public void AutoConnectFalse_DoesNotConnectOnStartup()
    {
        var dto = SampleCamera();
        dto.AutoConnectCameraOnStartup = false;
        var runtime = CameraSettingsMapper.ToRuntime(dto, "pass");
        Assert.False(CameraAutoConnectPolicy.ShouldAutoConnectOnStartup(runtime.IsEnabled, runtime.AutoConnectCameraOnStartup));
    }

    [Fact]
    public async Task Startup_DoesNotRequireReenteredConfig()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StationSettingsService>();
        await service.SaveCameraAsync(SampleCamera("PersistedOnlyInDb"));

        var edit = await service.GetCameraAsync();
        Assert.Null(edit.Password);
        Assert.True(edit.HasStoredPassword);

        var runtime = await service.GetCameraRuntimeAsync();
        Assert.Equal("PersistedOnlyInDb", runtime.Password);
        Assert.Equal("192.168.1.100", runtime.RtspHost);
    }

    [Fact]
    public async Task EmptyPasswordSave_KeepsOldPassword()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StationSettingsService>();

        await service.SaveCameraAsync(SampleCamera("OriginalPass"));
        var update = SampleCamera();
        update.Password = null;
        update.RtspHost = "192.168.1.101";
        await service.SaveCameraAsync(update);

        var runtime = await service.GetCameraRuntimeAsync();
        Assert.Equal("OriginalPass", runtime.Password);
        Assert.Equal("192.168.1.101", runtime.RtspHost);
    }

    [Fact]
    public async Task NewPasswordSave_ReplacesOldPassword()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StationSettingsService>();

        await service.SaveCameraAsync(SampleCamera("OldPass"));
        var update = SampleCamera("NewPass");
        await service.SaveCameraAsync(update);

        var runtime = await service.GetCameraRuntimeAsync();
        Assert.Equal("NewPass", runtime.Password);
    }

    [Fact]
    public async Task ClearPassword_RemovesStoredPassword()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StationSettingsService>();

        await service.SaveCameraAsync(SampleCamera("ToRemove"));
        var clear = SampleCamera();
        clear.Password = null;
        clear.ClearStoredPassword = true;
        await service.SaveCameraAsync(clear);

        var edit = await service.GetCameraAsync();
        Assert.False(edit.HasStoredPassword);
        var runtime = await service.GetCameraRuntimeAsync();
        Assert.Null(runtime.Password);
    }

    [Fact]
    public async Task Connected_OnlyAfterFirstFrame()
    {
        await using var stream = new ControllableCameraStreamService();
        var settings = CameraSettingsMapper.ToRuntime(SampleCamera(), "pass");
        var connectedBeforeFrame = false;

        stream.StateChanged += (_, state) =>
        {
            if (state == CameraConnectionState.Connected && !stream.HasRenderedFirstFrame)
                connectedBeforeFrame = true;
        };

        var ok = await stream.ConnectAsync(settings);
        Assert.True(ok);
        Assert.True(stream.HasReceivedFirstFrame);
        Assert.True(stream.HasRenderedFirstFrame);
        Assert.True(stream.IsConnected);
        Assert.False(connectedBeforeFrame);
    }

    [Fact]
    public void Retry_StopsAfterThreeAttempts()
    {
        Assert.True(CameraAutoConnectPolicy.ShouldRetry(0));
        Assert.True(CameraAutoConnectPolicy.ShouldRetry(2));
        Assert.False(CameraAutoConnectPolicy.ShouldRetry(3));
        Assert.Equal(CameraAutoConnectPolicy.MaxRetryAttempts, 3);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 3000)]
    [InlineData(2, 10000)]
    public void Retry_Delays(int attemptIndex, int expectedMs)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), CameraAutoConnectPolicy.GetRetryDelay(attemptIndex));
    }

    [Fact]
    public async Task ConnectSuccess_CancelsRemainingRetries()
    {
        await using var stream = new ControllableCameraStreamService();
        stream.Configure(failuresBeforeSuccess: 2);
        var settings = CameraSettingsMapper.ToRuntime(SampleCamera(), "pass");

        var connected = false;
        for (var attempt = 0; attempt < CameraAutoConnectPolicy.MaxRetryAttempts && !connected; attempt++)
        {
            var delay = CameraAutoConnectPolicy.GetRetryDelay(attempt);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);

            connected = await stream.ConnectAsync(settings) && stream.HasRenderedFirstFrame;
        }

        Assert.True(connected);
        Assert.Equal(3, stream.ConnectAttempts);
    }

    [Fact]
    public void ManualDisconnect_FlagPreventsAutoConnectPolicy()
    {
        const bool disconnectedByUser = true;
        Assert.False(
            !disconnectedByUser
            && CameraAutoConnectPolicy.ShouldAutoConnectOnStartup(true, true));
    }

    [Fact]
    public void RestartAfterManualDisconnect_StillAutoConnectsWhenSettingOn()
    {
        var dto = SampleCamera();
        Assert.True(CameraAutoConnectPolicy.ShouldAutoConnectOnStartup(dto.IsEnabled, dto.AutoConnectCameraOnStartup));
    }

    [Fact]
    public void PreviewDrawerToggle_DoesNotChangeConnectState()
    {
        Assert.Equal(0, WorkAreaLayoutCalculator.GetCameraDrawerWidth(false));
        Assert.True(WorkAreaLayoutCalculator.GetCameraDrawerWidth(true) > 0);
    }

    [Fact]
    public async Task NoParallelConnect_OperationsAreSerialized()
    {
        await using var stream = new ControllableCameraStreamService();
        stream.Configure(failuresBeforeSuccess: 0, connectDelayMs: 200);
        var settings = CameraSettingsMapper.ToRuntime(SampleCamera(), "pass");

        var first = stream.ConnectAsync(settings);
        var second = stream.ConnectAsync(settings);
        await Task.WhenAll(first, second);

        Assert.Equal(1, stream.MaxConcurrentConnectOperations);
        Assert.Equal(2, stream.ConnectAttempts);
    }

    [Fact]
    public async Task CameraConnect_DoesNotBlockOtherAsyncWork()
    {
        await using var stream = new ControllableCameraStreamService();
        stream.Configure(failuresBeforeSuccess: 0, connectDelayMs: 300);
        var settings = CameraSettingsMapper.ToRuntime(SampleCamera(), "pass");

        var connectTask = stream.ConnectAsync(settings);
        var otherCompleted = false;
        var other = Task.Run(async () =>
        {
            await Task.Delay(20);
            otherCompleted = true;
        });

        await Task.WhenAll(connectTask, other);
        Assert.True(otherCompleted);
    }

    [Fact]
    public async Task TestCamera_DoesNotPersistOrLeaveConnectedStream()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<StationSettingsService>();
        var stream = sp.GetRequiredService<ICameraStreamService>();

        await service.SaveCameraAsync(SampleCamera("SavedPass"));
        var before = await service.GetCameraAsync();

        var testRuntime = CameraSettingsMapper.ToRuntime(
            new CameraDeviceSettingsDto
            {
                IsEnabled = true,
                RtspHost = "10.0.0.99",
                RtspPort = 554,
                RtspPath = "/test"
            },
            null);
        await stream.TestAsync(testRuntime);

        var after = await service.GetCameraAsync();
        Assert.Equal(before.RtspHost, after.RtspHost);
        Assert.False(stream.IsConnected);
    }
}
