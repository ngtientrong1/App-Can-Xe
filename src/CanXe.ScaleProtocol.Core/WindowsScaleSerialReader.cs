using System.IO.Ports;
using System.Threading;

namespace CanXe.ScaleProtocol.Core;

public sealed class WindowsScaleSerialReader : IScaleSerialReader
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly ScaleFrameParser _parser = new();
    private ScaleStabilityDetector _stability;
    private readonly Timer _staleTimer;
    private SerialPort? _port;
    private ScaleConnectionState _connectionState = ScaleConnectionState.Disconnected;
    private ScaleReading? _latestReading;
    private DateTimeOffset? _lastValidFrameAt;
    private bool _isStale = true;
    private string? _lastError;
    private bool _disposed;

    public WindowsScaleSerialReader(ScaleSerialSettings? settings = null)
    {
        Settings = settings ?? new ScaleSerialSettings();
        _stability = CreateStabilityDetector();
        ScaleDiagnosticsLogger.VerboseFramesEnabled = Settings.VerboseFrameLogging;
        _staleTimer = new Timer(_ => CheckStale(), null, Timeout.Infinite, Timeout.Infinite);
    }

    private ScaleStabilityDetector CreateStabilityDetector() =>
        new(new ScaleStabilityOptions { ScaleDivisionKg = Settings.ScaleDivisionKg });

    public ScaleConnectionState ConnectionState
    {
        get { lock (_sync) return _connectionState; }
    }

    public ScaleSerialSettings Settings { get; private set; }
    public ScaleParserStatistics ParserStatistics => _parser.Statistics;
    public ScaleStabilityState StabilityState => _stability.CurrentState;
    public ScaleReading? LatestReading
    {
        get { lock (_sync) return _latestReading; }
    }

    public DateTimeOffset? LastValidFrameAt
    {
        get { lock (_sync) return _lastValidFrameAt; }
    }

    public bool IsStale
    {
        get { lock (_sync) return _isStale; }
    }

    public string? LastError
    {
        get { lock (_sync) return _lastError; }
    }

    public bool IsPortOpen
    {
        get { lock (_sync) return _port?.IsOpen == true; }
    }

    public event EventHandler<ScaleReading>? ValidReadingReceived;
    public event EventHandler<ScaleConnectionState>? ConnectionStateChanged;
    public event EventHandler? DiagnosticsChanged;

    public void UpdateSettings(ScaleSerialSettings settings)
    {
        if (_port?.IsOpen == true)
            throw new InvalidOperationException("Không thể thay đổi cấu hình khi cổng đang mở.");

        Settings = settings;
        ScaleDiagnosticsLogger.VerboseFramesEnabled = settings.VerboseFrameLogging;
    }

    public void ResetSession()
    {
        if (_port?.IsOpen == true)
            throw new InvalidOperationException("Không thể reset phiên khi cổng đang mở.");

        ResetSessionCore();
    }

    public async Task ReconfigureAndConnectAsync(
        ScaleSerialSettings settings,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectUnlockedAsync(cancellationToken).ConfigureAwait(false);
            Settings = settings;
            ResetSessionCore();
            await ConnectCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ConnectCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private Task ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        if (_port?.IsOpen == true)
            return Task.CompletedTask;

        SetConnectionState(ScaleConnectionState.Connecting);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _port = new SerialPort
                {
                    PortName = Settings.PortName,
                    BaudRate = Settings.BaudRate,
                    DataBits = Settings.DataBits,
                    Parity = ParseParity(Settings.Parity),
                    StopBits = ParseStopBits(Settings.StopBits),
                    Handshake = ParseHandshake(Settings.Handshake),
                    ReadTimeout = Settings.ReadTimeout,
                    WriteTimeout = Settings.ReadTimeout,
                    DtrEnable = false,
                    RtsEnable = false
                };
                _port.DataReceived += OnDataReceived;
                _port.ErrorReceived += OnErrorReceived;
                try
                {
                    _port.Open();
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _port.DataReceived -= OnDataReceived;
                    _port.ErrorReceived -= OnErrorReceived;
                    _port.Dispose();
                    _port = null;
                    SetConnectionState(ScaleConnectionState.Error);
                    throw new InvalidOperationException(ex.Message, ex);
                }
            }

            SetConnectionState(ScaleConnectionState.Connected);
            _staleTimer.Change(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
        }, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private Task DisconnectUnlockedAsync(CancellationToken cancellationToken = default)
    {
        _staleTimer.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.Run(() =>
        {
            lock (_sync)
            {
                if (_port is null)
                    return;

                _port.DataReceived -= OnDataReceived;
                _port.ErrorReceived -= OnErrorReceived;
                if (_port.IsOpen)
                    _port.Close();
                _port.Dispose();
                _port = null;
            }

            _parser.ResetBuffer();
            _stability.Reset();
            _latestReading = null;
            _lastValidFrameAt = null;
            _isStale = true;
            SetConnectionState(ScaleConnectionState.Disconnected);
            RaiseDiagnosticsChanged();
        }, cancellationToken);
    }

    private void ResetSessionCore()
    {
        _parser.ResetBuffer();
        _stability = CreateStabilityDetector();
        lock (_sync)
        {
            _latestReading = null;
            _lastValidFrameAt = null;
            _isStale = true;
            _lastError = null;
        }

        RaiseDiagnosticsChanged();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _staleTimer.Dispose();
        try
        {
            DisconnectUnlockedAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Best effort on shutdown.
        }

        _connectionGate.Dispose();
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        byte[] buffer;
        lock (_sync)
        {
            if (_port is null || !_port.IsOpen)
                return;

            try
            {
                var count = _port.BytesToRead;
                if (count <= 0)
                    return;

                buffer = new byte[count];
                var read = _port.Read(buffer, 0, count);
                if (read <= 0)
                    return;

                if (read != buffer.Length)
                    Array.Resize(ref buffer, read);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                _lastError = ex.Message;
                SetConnectionState(ScaleConnectionState.Error);
                return;
            }
        }

        var receivedAt = DateTimeOffset.Now;
        var readings = _parser.Append(buffer, receivedAt, _stability);
        foreach (var reading in readings)
            PublishReading(reading);
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        lock (_sync)
            _lastError = $"Serial error: {e.EventType}";
        SetConnectionState(ScaleConnectionState.Error);
    }

    private void PublishReading(ScaleReading reading)
    {
        ScaleDiagnosticsLogger.LogFrame(reading, reading.FrameInterval);

        lock (_sync)
        {
            _latestReading = reading;
            _lastValidFrameAt = reading.ReceivedAt;
            _isStale = false;
        }

        ValidReadingReceived?.Invoke(this, reading);
        RaiseDiagnosticsChanged();
    }

    private void CheckStale()
    {
        lock (_sync)
        {
            if (_connectionState != ScaleConnectionState.Connected)
                return;

            if (_lastValidFrameAt is null)
            {
                if (!_isStale)
                {
                    _isStale = true;
                    RaiseDiagnosticsChanged();
                }

                return;
            }

            var stale = (DateTimeOffset.Now - _lastValidFrameAt.Value).TotalMilliseconds
                > ScaleProtocolConstants.StaleTimeoutMilliseconds;
            if (stale == _isStale)
                return;

            _isStale = stale;
        }

        RaiseDiagnosticsChanged();
    }

    private void SetConnectionState(ScaleConnectionState state)
    {
        lock (_sync)
            _connectionState = state;
        ConnectionStateChanged?.Invoke(this, state);
        RaiseDiagnosticsChanged();
    }

    private void RaiseDiagnosticsChanged() => DiagnosticsChanged?.Invoke(this, EventArgs.Empty);

    private static Parity ParseParity(string value) =>
        Enum.TryParse<Parity>(value, true, out var parsed) ? parsed : Parity.None;

    private static StopBits ParseStopBits(string value) =>
        Enum.TryParse<StopBits>(value, true, out var parsed) ? parsed : StopBits.One;

    private static Handshake ParseHandshake(string value) =>
        Enum.TryParse<Handshake>(value, true, out var parsed) ? parsed : Handshake.None;
}
