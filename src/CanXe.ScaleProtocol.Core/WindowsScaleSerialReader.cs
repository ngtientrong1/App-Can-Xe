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
    private volatile bool _suppressEvents;

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

        ObjectDisposedException.ThrowIf(_disposed, this);
        SetConnectionState(ScaleConnectionState.Connecting);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            lock (_sync)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(WindowsScaleSerialReader));

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
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException or InvalidOperationException)
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

            if (_disposed)
                return;

            SetConnectionState(ScaleConnectionState.Connected);
            try
            {
                _staleTimer.Change(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
            }
            catch (ObjectDisposedException)
            {
                // Shutting down.
            }
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
        try
        {
            _staleTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
            // Already disposing.
        }

        return Task.Run(() =>
        {
            ClosePortUnlocked(raiseEvents: !_suppressEvents && !_disposed);
        }, cancellationToken);
    }

    private void ClosePortUnlocked(bool raiseEvents)
    {
        SerialPort? port;
        lock (_sync)
        {
            port = _port;
            _port = null;
            _latestReading = null;
            _lastValidFrameAt = null;
            _isStale = true;
            _connectionState = ScaleConnectionState.Disconnected;
        }

        if (port is not null)
        {
            try
            {
                port.DataReceived -= OnDataReceived;
            }
            catch
            {
                // Ignore during teardown.
            }

            try
            {
                port.ErrorReceived -= OnErrorReceived;
            }
            catch
            {
                // Ignore during teardown.
            }

            try
            {
                if (port.IsOpen)
                    port.Close();
            }
            catch (Exception ex) when (
                ex is ObjectDisposedException
                    or IOException
                    or InvalidOperationException
                    or UnauthorizedAccessException)
            {
                // Expected when port is already gone during shutdown.
            }

            try
            {
                port.Dispose();
            }
            catch (Exception ex) when (
                ex is ObjectDisposedException
                    or IOException
                    or InvalidOperationException)
            {
                // Expected during shutdown.
            }
        }

        try
        {
            _parser.ResetBuffer();
            _stability.Reset();
        }
        catch
        {
            // Best effort.
        }

        if (raiseEvents)
        {
            ConnectionStateChanged?.Invoke(this, ScaleConnectionState.Disconnected);
            RaiseDiagnosticsChanged();
        }
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

        _suppressEvents = true;
        _disposed = true;

        try
        {
            _staleTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        catch
        {
            // Best effort.
        }

        try
        {
            _staleTimer.Dispose();
        }
        catch
        {
            // Best effort.
        }

        // Close synchronously without raising UI-bound events (avoids Dispatcher.Invoke deadlock).
        try
        {
            ClosePortUnlocked(raiseEvents: false);
        }
        catch
        {
            // Best effort on shutdown.
        }

        try
        {
            _connectionGate.Dispose();
        }
        catch
        {
            // Best effort.
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_disposed || _suppressEvents)
            return;

        byte[] buffer;
        lock (_sync)
        {
            if (_disposed || _port is null || !_port.IsOpen)
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
        if (_disposed || _suppressEvents)
            return;

        lock (_sync)
            _lastError = $"Serial error: {e.EventType}";
        SetConnectionState(ScaleConnectionState.Error);
    }

    private void PublishReading(ScaleReading reading)
    {
        if (_disposed || _suppressEvents)
            return;

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
        if (_disposed || _suppressEvents)
            return;

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
        if (_disposed || _suppressEvents)
        {
            lock (_sync)
                _connectionState = state;
            return;
        }

        lock (_sync)
            _connectionState = state;
        ConnectionStateChanged?.Invoke(this, state);
        RaiseDiagnosticsChanged();
    }

    private void RaiseDiagnosticsChanged()
    {
        if (_disposed || _suppressEvents)
            return;

        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Parity ParseParity(string value) =>
        Enum.TryParse<Parity>(value, true, out var parsed) ? parsed : Parity.None;

    private static StopBits ParseStopBits(string value) =>
        Enum.TryParse<StopBits>(value, true, out var parsed) ? parsed : StopBits.One;

    private static Handshake ParseHandshake(string value) =>
        Enum.TryParse<Handshake>(value, true, out var parsed) ? parsed : Handshake.None;
}
