using CanXe.ScaleProtocol.Core;

namespace CanXe.Tests.Support;

public sealed class FakeScaleSerialReader : IScaleSerialReader
{
    private ScaleConnectionState _connectionState = ScaleConnectionState.Disconnected;
    private ScaleReading? _latestReading;
    private DateTimeOffset? _lastValidFrameAt;
    private bool _isStale = true;
    private string? _lastError;
    private bool _connectShouldFail;
    private bool _disposed;

    public bool ConnectShouldFail
    {
        get => _connectShouldFail;
        set => _connectShouldFail = value;
    }

    public int UpdateSettingsCallCount { get; private set; }
    public int ResetSessionCallCount { get; private set; }
    public int ConnectCallCount { get; private set; }
    public int DisconnectCallCount { get; private set; }
    public ScaleSerialSettings LastAppliedSettings { get; private set; } = new();

    public ScaleConnectionState ConnectionState => _connectionState;
    public ScaleSerialSettings Settings { get; private set; } = new();
    public ScaleParserStatistics ParserStatistics { get; } = new();
    public ScaleStabilityState StabilityState { get; private set; } = new() { IsStable = false };
    public ScaleReading? LatestReading => _latestReading;
    public DateTimeOffset? LastValidFrameAt => _lastValidFrameAt;
    public bool IsStale => _isStale;
    public string? LastError => _lastError;

    public event EventHandler<ScaleReading>? ValidReadingReceived;
    public event EventHandler<ScaleConnectionState>? ConnectionStateChanged;
    public event EventHandler? DiagnosticsChanged;

    public void ConfigureStableReading(long weightKg, byte[] rawFrame)
    {
        _latestReading = new ScaleReading
        {
            WeightKg = weightKg,
            RawFrame = rawFrame,
            IsStable = true,
            ReceivedAt = DateTimeOffset.Now
        };
        StabilityState = new ScaleStabilityState
        {
            IsStable = true,
            ConsecutiveMatchingFrames = 8,
            CurrentWeightKg = weightKg
        };
        _lastValidFrameAt = DateTimeOffset.Now;
        _isStale = false;
    }

    public void SetStale(bool stale) => _isStale = stale;

    public void UpdateSettings(ScaleSerialSettings settings)
    {
        if (_connectionState == ScaleConnectionState.Connected)
            throw new InvalidOperationException("Không thể thay đổi cấu hình khi cổng đang mở.");

        UpdateSettingsCallCount++;
        LastAppliedSettings = settings;
        Settings = settings;
    }

    public void ResetSession()
    {
        if (_connectionState == ScaleConnectionState.Connected)
            throw new InvalidOperationException("Không thể reset phiên khi cổng đang mở.");

        ResetSessionCallCount++;
        _latestReading = null;
        _lastValidFrameAt = null;
        _isStale = true;
        StabilityState = new ScaleStabilityState { IsStable = false };
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ConnectCallCount++;
        if (_connectShouldFail)
        {
            _connectionState = ScaleConnectionState.Error;
            _lastError = "Connect failed";
            ConnectionStateChanged?.Invoke(this, _connectionState);
            return Task.CompletedTask;
        }

        _connectionState = ScaleConnectionState.Connected;
        ConnectionStateChanged?.Invoke(this, _connectionState);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        DisconnectCallCount++;
        _connectionState = ScaleConnectionState.Disconnected;
        _latestReading = null;
        _isStale = true;
        ConnectionStateChanged?.Invoke(this, _connectionState);
        return Task.CompletedTask;
    }

    public void PublishReading(ScaleReading reading)
    {
        _latestReading = reading;
        _lastValidFrameAt = reading.ReceivedAt;
        _isStale = false;
        ValidReadingReceived?.Invoke(this, reading);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _connectionState = ScaleConnectionState.Disconnected;
    }
}
