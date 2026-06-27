using CanXe.ScaleProtocol.Core;

namespace CanXe.Tests.Support;

public sealed class FakeScaleSerialReader : IScaleSerialReader
{
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private ScaleConnectionState _connectionState = ScaleConnectionState.Disconnected;
    private ScaleReading? _latestReading;
    private DateTimeOffset? _lastValidFrameAt;
    private bool _isStale = true;
    private string? _lastError;
    private bool _connectShouldFail;
    private bool _disposed;
    private int _concurrentConnectAttempts;

    public bool ConnectShouldFail
    {
        get => _connectShouldFail;
        set => _connectShouldFail = value;
    }

    public TimeSpan ConnectDelay { get; set; } = TimeSpan.Zero;

    public int UpdateSettingsCallCount { get; private set; }
    public int ResetSessionCallCount { get; private set; }
    public int ConnectCallCount { get; private set; }
    public int DisconnectCallCount { get; private set; }
    public int ReconfigureAndConnectCallCount { get; private set; }
    public int MaxConcurrentConnectAttempts { get; private set; }
    public ScaleSerialSettings LastAppliedSettings { get; private set; } = new();

    public ScaleConnectionState ConnectionState => _connectionState;
    public ScaleSerialSettings Settings { get; private set; } = new();
    public ScaleParserStatistics ParserStatistics { get; } = new();
    public ScaleStabilityState StabilityState { get; private set; } = new() { IsStable = false };
    public ScaleReading? LatestReading => _latestReading;
    public DateTimeOffset? LastValidFrameAt => _lastValidFrameAt;
    public bool IsStale => _isStale;
    public string? LastError => _lastError;
    public bool IsPortOpen =>
        _connectionState is ScaleConnectionState.Connected or ScaleConnectionState.Connecting;

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
        if (IsPortOpen)
            throw new InvalidOperationException("Không thể thay đổi cấu hình khi cổng đang mở.");

        UpdateSettingsCallCount++;
        LastAppliedSettings = settings;
        Settings = settings;
    }

    public void ResetSession()
    {
        if (IsPortOpen)
            throw new InvalidOperationException("Không thể reset phiên khi cổng đang mở.");

        ResetSessionCallCount++;
        _latestReading = null;
        _lastValidFrameAt = null;
        _isStale = true;
        StabilityState = new ScaleStabilityState { IsStable = false };
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ReconfigureAndConnectAsync(
        ScaleSerialSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            ReconfigureAndConnectCallCount++;
            await DisconnectAsync(cancellationToken);
            Settings = settings;
            LastAppliedSettings = settings;
            UpdateSettingsCallCount++;
            ResetSessionCallCount++;
            await ConnectAsync(cancellationToken);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var active = Interlocked.Increment(ref _concurrentConnectAttempts);
        try
        {
            MaxConcurrentConnectAttempts = Math.Max(MaxConcurrentConnectAttempts, active);
            ConnectCallCount++;
            _connectionState = ScaleConnectionState.Connecting;
            ConnectionStateChanged?.Invoke(this, _connectionState);

            if (ConnectDelay > TimeSpan.Zero)
                await Task.Delay(ConnectDelay, cancellationToken);

            if (_connectShouldFail)
            {
                _connectionState = ScaleConnectionState.Error;
                _lastError = "Connect failed";
                ConnectionStateChanged?.Invoke(this, _connectionState);
                return;
            }

            _connectionState = ScaleConnectionState.Connected;
            ConnectionStateChanged?.Invoke(this, _connectionState);
        }
        finally
        {
            Interlocked.Decrement(ref _concurrentConnectAttempts);
        }
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
        _connectionGate.Dispose();
    }
}
