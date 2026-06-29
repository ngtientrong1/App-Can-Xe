using CanXe.Application.Interfaces;
using CanXe.Domain.Models;
using CanXe.ScaleProtocol.Core;

namespace CanXe.Infrastructure.Scale;

public sealed class CompositeScaleService : IScaleService, IHardwareScaleDiagnostics
{
    private readonly SimulatedScaleService _simulated = new();
    private readonly IScaleSerialReader _reader;
    private readonly SemaphoreSlim _modeLock = new(1, 1);
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private bool _disposed;
    private ScaleInputMode _inputMode = ScaleInputMode.SimulationAutomatic;
    private string? _lastChecksumError;

    public CompositeScaleService(IScaleSerialReader? reader = null)
    {
        _reader = reader ?? new WindowsScaleSerialReader();
        _simulated.WeightChanged += (_, weightKg) =>
        {
            if (_inputMode != ScaleInputMode.Hardware)
                WeightChanged?.Invoke(this, weightKg);
        };
        WireReaderEvents();
    }

    public event EventHandler<decimal>? WeightChanged;
    public event EventHandler? HardwareDiagnosticsChanged;

    public ScaleInputMode InputMode => _inputMode;

    public bool IsManualMode => _inputMode == ScaleInputMode.SimulationManual && _simulated.IsManualMode;

    public ScaleConnectionState ConnectionState => _reader.ConnectionState;
    public bool IsConnected => _reader.ConnectionState == ScaleConnectionState.Connected;
    public bool IsStale => _reader.IsStale;
    public bool IsStable => _reader.StabilityState.IsStable;
    public ScaleStableSource StableSource =>
        _reader.LatestReading?.StableSource ?? ScaleStableSource.Unknown;
    public DateTimeOffset? LastValidFrameAt => _reader.LastValidFrameAt;
    public string? LastChecksumError => _lastChecksumError;
    public ScaleReading? LatestReading => _reader.LatestReading;
    public int ValidFrameCount => _reader.ParserStatistics.ValidFrames;
    public int InvalidFrameCount => _reader.ParserStatistics.InvalidFrames;
    public long DiscardedBytes => _reader.ParserStatistics.DiscardedBytes;
    public ScaleSerialSettings HardwareSettings => _reader.Settings;

    public void SetInputMode(ScaleInputMode mode) =>
        SetInputModeAsync(mode).GetAwaiter().GetResult();

    public async Task SetInputModeAsync(ScaleInputMode mode, CancellationToken cancellationToken = default)
    {
        await _modeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_inputMode == mode)
                return;

            var previous = _inputMode;

            if (previous == ScaleInputMode.Hardware)
                await DisconnectHardwareAsync(cancellationToken).ConfigureAwait(false);

            _inputMode = mode;

            if (mode == ScaleInputMode.Hardware)
            {
                await _simulated.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _simulated.StartAsync(cancellationToken).ConfigureAwait(false);
                if (mode == ScaleInputMode.SimulationAutomatic)
                {
                    _simulated.ResumeAutomaticSimulation();
                    _simulated.SetManualMode(false);
                }
                else
                {
                    _simulated.SetManualMode(true);
                }
            }

            HardwareDiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _modeLock.Release();
        }
    }

    public void UpdateHardwareSettings(ScaleSerialSettings settings)
    {
        if (_reader.IsPortOpen)
            throw new InvalidOperationException("Không thể thay đổi cấu hình khi cổng đang mở.");

        _reader.UpdateSettings(settings);
    }

    public Task ConnectHardwareAsync(CancellationToken cancellationToken = default) =>
        PrepareAndConnectHardwareAsync(_reader.Settings, cancellationToken);

    public async Task PrepareAndConnectHardwareAsync(
        ScaleSerialSettings settings,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_inputMode != ScaleInputMode.Hardware)
            throw new InvalidOperationException("Chỉ có thể kết nối COM khi nguồn đầu cân là Hardware.");

        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _reader.ReconfigureAndConnectAsync(settings, cancellationToken).ConfigureAwait(false);

            if (_reader.ConnectionState != ScaleConnectionState.Connected)
                throw new InvalidOperationException(_reader.LastError ?? "Kết nối thất bại.");
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async Task DisconnectHardwareAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _reader.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_inputMode == ScaleInputMode.Hardware)
            return Task.CompletedTask;

        return _simulated.StartAsync(cancellationToken);
    }

    public Task<decimal> GetCurrentWeightAsync(CancellationToken cancellationToken = default)
    {
        if (_inputMode == ScaleInputMode.Hardware)
        {
            if (!IsConnected || IsStale || _reader.LatestReading is null)
                throw new InvalidOperationException("Đầu cân COM chưa sẵn sàng.");
            return Task.FromResult((decimal)_reader.LatestReading.WeightKg);
        }

        return _simulated.GetCurrentWeightAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await DisconnectHardwareAsync(cancellationToken).ConfigureAwait(false);
        await _simulated.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public void SetManualMode(bool enabled)
    {
        _simulated.SetManualMode(enabled);
        if (_inputMode == ScaleInputMode.SimulationManual)
            WeightChanged?.Invoke(this, _simulated.GetCurrentWeightAsync().GetAwaiter().GetResult());
    }

    public void SetManualWeightKg(decimal weightKg)
    {
        _simulated.SetManualWeightKg(weightKg);
        if (_inputMode == ScaleInputMode.SimulationManual)
            WeightChanged?.Invoke(this, weightKg);
    }

    public void ResumeAutomaticSimulation() => _simulated.ResumeAutomaticSimulation();

    public bool CanCaptureWeight()
    {
        if (_inputMode != ScaleInputMode.Hardware)
            return true;

        return IsConnected && !IsStale && _reader.LatestReading is not null && IsStable;
    }

    public string? GetHardwareCaptureBlockReason()
    {
        if (_inputMode != ScaleInputMode.Hardware)
            return null;
        if (!IsConnected)
            return "Chưa kết nối đầu cân COM.";
        if (IsStale)
            return "Dữ liệu đầu cân đã cũ — không có frame hợp lệ gần đây.";
        if (_reader.LatestReading is null)
            return "Chưa nhận frame hợp lệ từ đầu cân.";
        if (!IsStable)
        {
            var source = LatestReading?.StableSource ?? ScaleStableSource.Unknown;
            return source == ScaleStableSource.HardwareFlag
                ? "Trọng lượng chưa ổn định — đầu cân báo chưa stable."
                : "Trọng lượng chưa ổn định — chờ thêm frame giống nhau.";
        }
        return null;
    }

    public string? GetLatestFrameDisplay() => ScaleFrameDisplay.FormatLatestFrame(_reader.LatestReading);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _reader.ValidReadingReceived -= OnHardwareReading;
        _reader.ConnectionStateChanged -= OnReaderStateChanged;
        _reader.DiagnosticsChanged -= OnReaderDiagnosticsChanged;
        await StopAsync().ConfigureAwait(false);
        await _simulated.DisposeAsync().ConfigureAwait(false);
        _reader.Dispose();
        _modeLock.Dispose();
        _connectionGate.Dispose();
    }

    internal void WireReaderEvents()
    {
        _reader.ValidReadingReceived += OnHardwareReading;
        _reader.ConnectionStateChanged += OnReaderStateChanged;
        _reader.DiagnosticsChanged += OnReaderDiagnosticsChanged;
    }

    private void OnHardwareReading(object? sender, ScaleReading reading)
    {
        if (_inputMode != ScaleInputMode.Hardware)
            return;

        _lastChecksumError = null;
        WeightChanged?.Invoke(this, reading.WeightKg);
        HardwareDiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnReaderStateChanged(object? sender, ScaleConnectionState e) =>
        HardwareDiagnosticsChanged?.Invoke(this, EventArgs.Empty);

    private void OnReaderDiagnosticsChanged(object? sender, EventArgs e) =>
        HardwareDiagnosticsChanged?.Invoke(this, EventArgs.Empty);
}
