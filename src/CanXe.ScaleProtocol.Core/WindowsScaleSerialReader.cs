using System.Threading;

namespace CanXe.ScaleProtocol.Core;

public sealed class WindowsScaleSerialReader : IScaleSerialReader
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly ISerialPortFactory _portFactory;
    private readonly ScaleFrameParser _parser = new();
    private ScaleStabilityDetector _stability;
    private readonly Timer _staleTimer;
    private readonly Timer _keepAliveTimer;
    private ISerialPortHandle? _port;
    private ScaleConnectionState _connectionState = ScaleConnectionState.Disconnected;
    private ScaleReading? _latestReading;
    private DateTimeOffset? _lastValidFrameAt;
    private bool _isStale = true;
    private string? _lastError;
    private bool _disposed;
    private volatile bool _suppressEvents;

    /// <summary>Bumped once per connect/disconnect operation. Lets a timed-out attempt whose
    /// blocking Open()/Close() eventually returns in the background detect that it has been
    /// superseded, so it never mutates state on behalf of a newer attempt.</summary>
    private int _attemptId;

    /// <summary>
    /// How often to poke the open port while connected. Purely to generate USB bus activity so
    /// Windows never considers a quiet-but-healthy connection idle enough for USB Selective
    /// Suspend — unrelated to <see cref="ScaleWatchdogPolicy"/>'s much longer no-data timeout,
    /// which detects an actually-dead connection instead.
    /// </summary>
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(2);

    public WindowsScaleSerialReader(ScaleSerialSettings? settings = null, ISerialPortFactory? portFactory = null)
    {
        Settings = settings ?? new ScaleSerialSettings();
        _portFactory = portFactory ?? new SystemSerialPortFactory();
        _stability = CreateStabilityDetector();
        ScaleDiagnosticsLogger.VerboseFramesEnabled = Settings.VerboseFrameLogging;
        _staleTimer = new Timer(_ => CheckStale(), null, Timeout.Infinite, Timeout.Infinite);
        _keepAliveTimer = new Timer(_ => KeepAlivePoke(), null, Timeout.Infinite, Timeout.Infinite);
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
            await RunWithHangProtectionAsync(
                id => DisconnectCoreAsync(id, cancellationToken),
                "đóng cổng (reconfigure)").ConfigureAwait(false);
            Settings = settings;
            ResetSessionCore();
            await RunWithHangProtectionAsync(
                id => ConnectCoreAsync(id, cancellationToken),
                "mở cổng").ConfigureAwait(false);
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
            await RunWithHangProtectionAsync(
                id => ConnectCoreAsync(id, cancellationToken),
                "mở cổng").ConfigureAwait(false);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    /// <summary>
    /// Awaits <paramref name="operation"/> but never blocks the caller (and therefore never blocks
    /// the shared <see cref="_connectionGate"/>) longer than <see cref="ScaleSerialSettings.OpenCloseTimeoutMs"/>.
    /// If the underlying blocking Open()/Close() hasn't returned by then, the reader flips to
    /// <see cref="ScaleConnectionState.Hung"/> and returns — the abandoned task is left to finish
    /// (or never finish) in the background under attempt-id guarding so it cannot corrupt state
    /// for whatever attempt runs next.
    /// </summary>
    private async Task RunWithHangProtectionAsync(Func<int, Task> operation, string label)
    {
        var attemptId = Interlocked.Increment(ref _attemptId);
        var task = operation(attemptId);
        var timeoutMs = Math.Max(500, Settings.OpenCloseTimeoutMs);
        var timeoutTask = Task.Delay(timeoutMs);
        var winner = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
        if (!ReferenceEquals(winner, task))
        {
            MarkHung(label);
            _ = ObserveAbandonedAsync(task);
            return;
        }

        await task.ConfigureAwait(false);
    }

    private static async Task ObserveAbandonedAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Nobody is awaiting this attempt anymore — the exception has nowhere useful to go.
        }
    }

    private void MarkHung(string label)
    {
        bool shouldRaise;
        lock (_sync)
        {
            _lastError = $"Cổng bị treo khi {label} — driver USB-to-Serial không phản hồi.";
            _connectionState = ScaleConnectionState.Hung;
            shouldRaise = !_disposed && !_suppressEvents;
        }

        if (shouldRaise)
        {
            ConnectionStateChanged?.Invoke(this, ScaleConnectionState.Hung);
            RaiseDiagnosticsChanged();
        }
    }

    private Task ConnectCoreAsync(int attemptId, CancellationToken cancellationToken)
    {
        if (_port?.IsOpen == true)
            return Task.CompletedTask;

        ObjectDisposedException.ThrowIf(_disposed, this);
        SetConnectionState(ScaleConnectionState.Connecting);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            ISerialPortHandle localPort;
            lock (_sync)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(WindowsScaleSerialReader));
                if (Volatile.Read(ref _attemptId) != attemptId)
                    return; // Superseded before we even started opening.

                localPort = _portFactory.Create(Settings);
                localPort.DataAvailable += OnDataAvailable;
                localPort.SerialErrorOccurred += OnSerialErrorOccurred;
                _port = localPort;
            }

            try
            {
                localPort.Open();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException or InvalidOperationException)
            {
                var isCurrent = Volatile.Read(ref _attemptId) == attemptId;
                lock (_sync)
                {
                    if (isCurrent)
                        _lastError = ex.Message;
                    if (ReferenceEquals(_port, localPort))
                        _port = null;
                }

                localPort.DataAvailable -= OnDataAvailable;
                localPort.SerialErrorOccurred -= OnSerialErrorOccurred;
                localPort.Dispose();

                if (!isCurrent)
                    return; // Abandoned attempt — nobody is listening for this failure anymore.

                SetConnectionState(ScaleConnectionState.Error);
                throw new InvalidOperationException(ex.Message, ex);
            }

            if (Volatile.Read(ref _attemptId) != attemptId)
            {
                // Open() finally succeeded, but only after we'd already given up on this attempt
                // (a newer attempt may already own _port). Quietly close what we just opened.
                try { localPort.DataAvailable -= OnDataAvailable; localPort.SerialErrorOccurred -= OnSerialErrorOccurred; } catch { /* best effort */ }
                try { if (localPort.IsOpen) localPort.Close(); } catch { /* best effort */ }
                try { localPort.Dispose(); } catch { /* best effort */ }
                lock (_sync)
                {
                    if (ReferenceEquals(_port, localPort))
                        _port = null;
                }
                return;
            }

            if (_disposed)
                return;

            SetConnectionState(ScaleConnectionState.Connected);
            try
            {
                _staleTimer.Change(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
                _keepAliveTimer.Change(KeepAliveInterval, KeepAliveInterval);
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
            await RunWithHangProtectionAsync(
                id => DisconnectCoreAsync(id, cancellationToken),
                "đóng cổng").ConfigureAwait(false);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private Task DisconnectCoreAsync(int attemptId, CancellationToken cancellationToken)
    {
        try
        {
            _staleTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _keepAliveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
            // Already disposing.
        }

        return Task.Run(() =>
        {
            ClosePortUnlocked(attemptId, raiseEvents: !_suppressEvents && !_disposed);
        }, cancellationToken);
    }

    /// <param name="attemptId">The attempt this close belongs to, or null to always apply
    /// (used only from <see cref="Dispose"/>, which must clean up unconditionally).</param>
    private void ClosePortUnlocked(int? attemptId, bool raiseEvents)
    {
        ISerialPortHandle? port;
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
                port.DataAvailable -= OnDataAvailable;
            }
            catch
            {
                // Ignore during teardown.
            }

            try
            {
                port.SerialErrorOccurred -= OnSerialErrorOccurred;
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

        // This close was abandoned (timed out) and has now finished late, after a newer attempt
        // has already started — don't touch shared parser/stability state or fire a stale event
        // on top of whatever the current attempt has since established.
        if (attemptId is not null && Volatile.Read(ref _attemptId) != attemptId)
            return;

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
        Interlocked.Increment(ref _attemptId);

        try
        {
            _staleTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _keepAliveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        catch
        {
            // Best effort.
        }

        try
        {
            _staleTimer.Dispose();
            _keepAliveTimer.Dispose();
        }
        catch
        {
            // Best effort.
        }

        // Close synchronously without raising UI-bound events (avoids Dispatcher.Invoke deadlock).
        try
        {
            ClosePortUnlocked(attemptId: null, raiseEvents: false);
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

    private void OnDataAvailable(object? sender, EventArgs e)
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

    private void OnSerialErrorOccurred(object? sender, string eventType)
    {
        if (_disposed || _suppressEvents)
            return;

        lock (_sync)
            _lastError = $"Serial error: {eventType}";
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

    /// <summary>
    /// Touches the open port on a short interval so a healthy-but-quiet connection still looks
    /// "in use" to Windows' USB power management, regardless of how long it's been since the scale
    /// itself last sent a frame. Deliberately queries a control line rather than writing to the
    /// port, so it can never inject bytes into the scale's own protocol stream.
    /// </summary>
    private void KeepAlivePoke()
    {
        if (_disposed)
            return;

        ISerialPortHandle? port;
        lock (_sync)
        {
            if (_disposed || _connectionState != ScaleConnectionState.Connected)
                return;
            port = _port;
        }

        if (port?.IsOpen == true)
            port.Poke();
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
}
