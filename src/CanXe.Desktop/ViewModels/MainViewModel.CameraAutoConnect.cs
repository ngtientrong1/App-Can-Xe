using System.Diagnostics;
using System.Windows.Media.Imaging;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Services;
using CanXe.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private const int PreviewMaxFps = 6;
    private static readonly TimeSpan PreviewMinInterval = TimeSpan.FromMilliseconds(1000.0 / PreviewMaxFps);

    private DateTimeOffset _lastPreviewRenderUtc;
    private DateTimeOffset? _lastValidPreviewAt;
    private volatile CameraDecodedFrame? _pendingPreviewFrame;
    private int _previewUpdatePending;
    private long _previewFramesReceived;
    private long _previewFramesRendered;
    private long _previewFramesDropped;

    [ObservableProperty] private bool _isCameraConnecting;
    [ObservableProperty] private string _cameraConnectionStatus = "Camera: Chưa cấu hình";
    [ObservableProperty] private BitmapSource? _cameraPreviewFrame;
    [ObservableProperty] private bool _isCameraReconnectOverlayVisible;
    [ObservableProperty] private string? _cameraLastFrameTimeText;

    public long PreviewFramesReceived => Interlocked.Read(ref _previewFramesReceived);
    public long PreviewFramesRendered => Interlocked.Read(ref _previewFramesRendered);
    public long PreviewFramesDropped => Interlocked.Read(ref _previewFramesDropped);
    public int PreviewUpdatePending => Volatile.Read(ref _previewUpdatePending);

    public bool IsRetryCameraConnectVisible =>
        Settings.CameraEnabled
        && !IsCameraConnecting
        && _cameraSupervisor?.State is CameraConnectionState.Failed or CameraConnectionState.Disconnected
        && _cameraSupervisor is { IsUserDisconnected: true };

    public bool IsCameraConnectEnabled =>
        Settings.CameraEnabled && !IsCameraConnecting && !_cameraStream.IsConnected;

    public bool IsCameraDisconnectEnabled =>
        Settings.CameraEnabled
        && (_cameraSupervisor?.State is not CameraConnectionState.Disconnected and not CameraConnectionState.Disabled || IsCameraConnecting);

    partial void OnIsCameraConnectingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRetryCameraConnectVisible));
        OnPropertyChanged(nameof(IsCameraConnectEnabled));
        OnPropertyChanged(nameof(IsCameraDisconnectEnabled));
    }

    private void WireCameraSupervisor(ICameraConnectionSupervisor supervisor)
    {
        supervisor.StateChanged += (_, state) =>
            RunOnUiThread(() =>
            {
                UpdateCameraStatusDisplay(state);
                IsCameraReconnectOverlayVisible = state is CameraConnectionState.Stalled or CameraConnectionState.Reconnecting;
                OnPropertyChanged(nameof(IsRetryCameraConnectVisible));
                OnPropertyChanged(nameof(IsCameraConnectEnabled));
                OnPropertyChanged(nameof(IsCameraDisconnectEnabled));
            });

        supervisor.HealthChanged += (_, _) =>
            RunOnUiThread(() => Settings.RefreshCameraHealth(supervisor.GetHealthSnapshot()));

        _cameraStream.FrameDecoded += (_, frame) => EnqueueCameraPreviewFrame(frame);

        _cameraStream.FirstFrameReceived += (_, _) =>
            RunOnUiThread(() => OnPropertyChanged(nameof(CameraPreviewAvailable)));
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }

    public async Task StartCameraSupervisorAsync(ICameraConnectionSupervisor supervisor)
    {
        await supervisor.StartAsync().ConfigureAwait(false);
    }

    public async Task AutoConnectCameraIfNeededAsync()
    {
        if (!ShouldAutoConnectCameraOnStartup())
        {
            UpdateCameraStatusDisplay();
            return;
        }

        await _cameraSupervisor.RequestConnectAsync(CameraConnectRequestSource.Startup).ConfigureAwait(false);
        UpdateCameraStatusDisplay();
    }

    private bool ShouldAutoConnectCameraOnStartup() =>
        CameraConnectionPolicy.ShouldAutoConnectOnStartup(
            EffectiveDeviceMode,
            Settings.CameraEnabled,
            Settings.AutoConnectCameraOnStartup)
        && _cameraSupervisor is { IsUserDisconnected: false };

    [RelayCommand]
    private async Task RetryConnectCameraAsync()
    {
        await _cameraSupervisor.RequestConnectAsync(CameraConnectRequestSource.Manual).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ConnectCameraAsync()
    {
        HeaderCameraStatus = "● Camera: Đang kết nối";
        await _cameraSupervisor.RequestConnectAsync(CameraConnectRequestSource.Manual).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task DisconnectCameraAsync()
    {
        await _cameraSupervisor.RequestDisconnectByUserAsync().ConfigureAwait(false);
        IsCameraConnecting = false;
        CameraPreviewFrame = null;
        CameraLastFrameTimeText = null;
        UpdateCameraStatusDisplay(CameraConnectionState.Disconnected);
    }

    public async Task ApplySavedCameraSettingsAsync()
    {
        var runtime = await _stationSettings.GetCameraRuntimeAsync().ConfigureAwait(false);
        if (!runtime.IsEnabled)
        {
            await _cameraSupervisor.RequestDisconnectByUserAsync().ConfigureAwait(false);
            CameraPreviewFrame = null;
            UpdateCameraStatusDisplay(CameraConnectionState.Disabled);
            return;
        }

        if (runtime.AutoConnectCameraOnStartup && !_cameraSupervisor.IsUserDisconnected)
        {
            await _cameraSupervisor.RequestConnectAsync(CameraConnectRequestSource.Settings).ConfigureAwait(false);
            return;
        }

        UpdateCameraStatusDisplay(CameraConnectionState.Disconnected);
    }

    private void UpdateCameraStatusDisplay(CameraConnectionState? stateOverride = null)
    {
        var state = stateOverride ?? _cameraSupervisor?.State ?? _cameraStream.State;

        if (!Settings.CameraEnabled)
        {
            HeaderCameraStatus = "● Camera: Tắt";
            CameraConnectionStatus = "Camera: Tắt";
            IsCameraReconnectOverlayVisible = false;
            return;
        }

        if (_cameraStream.IsConnected && state == CameraConnectionState.Connected)
        {
            HeaderCameraStatus = "● Camera: Đã kết nối";
            CameraConnectionStatus = "Camera: Đã kết nối";
            IsCameraReconnectOverlayVisible = false;
            return;
        }

        if (_cameraStream.HasReceivedFirstFrame && !_cameraStream.HasRenderedFirstFrame)
        {
            HeaderCameraStatus = "● Camera: Đang kết nối";
            CameraConnectionStatus = "Camera: Đang hiển thị hình ảnh";
            return;
        }

        switch (state)
        {
            case CameraConnectionState.Connecting:
                HeaderCameraStatus = "● Camera: Đang kết nối";
                CameraConnectionStatus = "Camera: Đang kết nối";
                IsCameraConnecting = true;
                return;
            case CameraConnectionState.Reconnecting:
                HeaderCameraStatus = "● Camera: Đang kết nối lại...";
                CameraConnectionStatus = "Camera: Đang kết nối lại...";
                IsCameraReconnectOverlayVisible = true;
                return;
            case CameraConnectionState.Stalled:
                HeaderCameraStatus = "● Camera: Luồng hình bị gián đoạn";
                CameraConnectionStatus = "Camera: Luồng hình bị gián đoạn";
                IsCameraReconnectOverlayVisible = true;
                return;
            case CameraConnectionState.Connected:
                HeaderCameraStatus = "● Camera: Đã kết nối";
                CameraConnectionStatus = "Camera: Đã kết nối";
                IsCameraConnecting = false;
                IsCameraReconnectOverlayVisible = false;
                return;
            case CameraConnectionState.Disconnected:
                HeaderCameraStatus = "● Camera: Chưa kết nối";
                CameraConnectionStatus = "Camera: Chưa kết nối";
                IsCameraConnecting = false;
                IsCameraReconnectOverlayVisible = false;
                return;
            case CameraConnectionState.Failed:
                HeaderCameraStatus = "● Camera: Mất kết nối";
                CameraConnectionStatus = _lastValidPreviewAt.HasValue
                    ? $"Camera: Mất kết nối — Ảnh cuối: {_lastValidPreviewAt.Value.LocalDateTime:HH:mm:ss}"
                    : "Camera: Mất kết nối";
                IsCameraReconnectOverlayVisible = false;
                return;
        }

        HeaderCameraStatus = "● Camera: Mất kết nối";
        CameraConnectionStatus = "Camera: Mất kết nối";
    }

    private void EnqueueCameraPreviewFrame(CameraDecodedFrame frame)
    {
        if (frame.SessionId != Guid.Empty && frame.SessionId != _cameraStream.ActiveSessionId)
            return;

        Interlocked.Increment(ref _previewFramesReceived);
        _pendingPreviewFrame = frame;

        if (Interlocked.CompareExchange(ref _previewUpdatePending, 1, 0) == 0)
            _ = ProcessPreviewQueueAsync();
    }

    private async Task ProcessPreviewQueueAsync()
    {
        try
        {
            while (true)
            {
                var frame = _pendingPreviewFrame;
                if (frame is null)
                    break;

                _pendingPreviewFrame = null;

                var now = DateTimeOffset.UtcNow;
                var wait = PreviewMinInterval - (now - _lastPreviewRenderUtc);
                if (wait > TimeSpan.Zero)
                {
                    if (_pendingPreviewFrame is not null)
                        Interlocked.Increment(ref _previewFramesDropped);
                    await Task.Delay(wait).ConfigureAwait(false);
                    continue;
                }

                var decodeSw = Stopwatch.StartNew();
                var bitmap = await Task.Run(() => CameraBitmapFactory.FromJpeg(frame.JpegBytes)).ConfigureAwait(false);
                decodeSw.Stop();
                if (bitmap is null)
                    continue;

                var renderedFrame = frame;
                RunOnUiThread(() =>
                {
                    CameraPreviewFrame = bitmap;
                    _lastValidPreviewAt = DateTimeOffset.Now;
                    _lastPreviewRenderUtc = DateTimeOffset.UtcNow;
                    CameraLastFrameTimeText = _lastValidPreviewAt.Value.ToString("HH:mm:ss");
                    _cameraStream.NotifyPreviewRendered(renderedFrame.Width, renderedFrame.Height);
                    Interlocked.Increment(ref _previewFramesRendered);
                    OnPropertyChanged(nameof(CameraPreviewAvailable));
                    UpdateCameraStatusDisplay(_cameraSupervisor.State);
                });

                if (_pendingPreviewFrame is null)
                    break;

                Interlocked.Increment(ref _previewFramesDropped);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _previewUpdatePending, 0);
            if (_pendingPreviewFrame is not null && Interlocked.CompareExchange(ref _previewUpdatePending, 1, 0) == 0)
                _ = ProcessPreviewQueueAsync();
        }
    }

    public bool CameraPreviewAvailable => CameraPreviewFrame is not null && _cameraStream.HasRenderedFirstFrame;
}
