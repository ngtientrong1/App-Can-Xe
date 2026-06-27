using System.Collections.Concurrent;
using CanXe.DeviceTester.Core.Formatting;
using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.Core.Services;

public sealed class SerialCaptureService : ISerialCaptureService
{
    private readonly ISerialPortProvider _portProvider;
    private readonly ISerialPortDiscoveryService _discoveryService;
    private readonly IRawSerialLogWriter _logWriter;
    private readonly object _sync = new();
    private readonly List<SerialDataChunk> _displayChunks = [];
    private readonly List<byte> _accumulatedBuffer = [];
    private readonly ConcurrentQueue<PendingUiUpdate> _pendingUiUpdates = new();
    private readonly List<DateTimeOffset> _recordingChunkTimes = [];
    private readonly DateTimeOffset _sessionStartedAt = DateTimeOffset.Now;

    private int _chunkSequence;
    private int _totalBytes;
    private int _dataReceivedCount;
    private DateTimeOffset? _lastDataAt;
    private DateTimeOffset? _rateWindowStart;
    private int _rateWindowBytes;
    private double _bytesPerSecondRecent;
    private SerialConnectionStatus _status = SerialConnectionStatus.Closed;
    private string? _statusMessage;
    private string _latestHexLine = string.Empty;
    private string _latestEscapedText = string.Empty;
    private string _eventChunksText = string.Empty;
    private SerialCaptureSession? _currentSession;
    private bool _portErrorDuringRecording;
    private bool _disposed;

    public SerialCaptureService(
        ISerialPortProvider portProvider,
        ISerialPortDiscoveryService discoveryService,
        IRawSerialLogWriter logWriter,
        SerialPortSettings? initialSettings = null)
    {
        _portProvider = portProvider;
        _discoveryService = discoveryService;
        _logWriter = logWriter;
        Settings = initialSettings ?? SerialPortSettings.CreateDefault();
        _portProvider.DataReceived += OnProviderDataReceived;
        _portProvider.ReadFailed += OnProviderReadFailed;
    }

    public SerialConnectionStatus Status => _status;
    public SerialPortSettings Settings { get; private set; }
    public bool IsPortOpen => _portProvider.IsOpen;
    public bool IsRecording => _logWriter.IsRecording;
    public bool SaveRawBinary
    {
        get => _logWriter.SaveRawBinary;
        set => _logWriter.SaveRawBinary = value;
    }
    public bool SaveSessionJson
    {
        get => _logWriter.SaveSessionJson;
        set => _logWriter.SaveSessionJson = value;
    }
    public bool IsDisplayPaused { get; set; }
    public string? StatusMessage => _statusMessage;
    public int TotalBytesReceived => _totalBytes;
    public int DataReceivedCount => _dataReceivedCount;
    public DateTimeOffset? LastDataReceivedAt => _lastDataAt;
    public double BytesPerSecondRecent => _bytesPerSecondRecent;
    public TimeSpan SessionElapsed => DateTimeOffset.Now - _sessionStartedAt;
    public IReadOnlyList<SerialDataChunk> DisplayChunks => _displayChunks;
    public byte[] AccumulatedBuffer => _accumulatedBuffer.ToArray();
    public string LatestHexLine => _latestHexLine;
    public string LatestEscapedText => _latestEscapedText;
    public string EventChunksText => _eventChunksText;
    public SerialCaptureSession? CurrentSession => _currentSession;
    public DateTimeOffset? RecordingStartedAt => _logWriter.RecordingStartedAt;
    public int RecordingTotalBytes => _logWriter.RecordingTotalBytes;
    public int RecordingChunkCount => _logWriter.RecordingChunkCount;
    public long LastChunkNumber => _logWriter.LastChunkNumber;
    public IReadOnlyList<byte> RecordingUniqueBytes => _logWriter.RecordingUniqueBytes;
    public DateTimeOffset? RecordingEndedAt => _logWriter.RecordingEndedAt;
    public bool PortErrorDuringRecording => _portErrorDuringRecording;

    public event EventHandler? StateChanged;
    public event EventHandler<byte[]>? RawBytesReceived;

    public double RecordingLongestNoDataGapMs
    {
        get
        {
            lock (_sync)
            {
                if (_recordingChunkTimes.Count < 2)
                    return 0;

                var max = 0.0;
                for (var i = 1; i < _recordingChunkTimes.Count; i++)
                {
                    var gap = (_recordingChunkTimes[i] - _recordingChunkTimes[i - 1]).TotalMilliseconds;
                    if (gap > max)
                        max = gap;
                }

                return max;
            }
        }
    }

    public IReadOnlyList<CaptureEventMarker> EventMarkers => _logWriter.EventMarkers;

    public IReadOnlyList<string> ScanPorts() => _discoveryService.GetAvailablePortNames();

    public void UpdateSettings(SerialPortSettings settings)
    {
        if (IsPortOpen)
            throw new InvalidOperationException("Không thể thay đổi cấu hình khi cổng đang mở.");
        Settings = settings;
        NotifyStateChanged();
    }

    public async Task OpenPortAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsPortOpen)
            return;

        SetStatus(SerialConnectionStatus.Connecting, $"Đang kết nối {Settings.PortName}...");
        try
        {
            await _portProvider.OpenAsync(Settings, cancellationToken).ConfigureAwait(false);
            SetStatus(SerialConnectionStatus.Open, $"Đã mở {Settings.PortName}.");
            _ = Task.Run(MonitorNoDataAsync, cancellationToken);
        }
        catch (Exception ex)
        {
            var mapped = SerialPortExceptionMapper.Map(ex, Settings.PortName);
            SetStatus(mapped.Status, mapped.Message);
            throw;
        }
    }

    public async Task ClosePortAsync(CancellationToken cancellationToken = default)
    {
        if (!IsPortOpen)
        {
            SetStatus(SerialConnectionStatus.Closed, "Cổng đang đóng.");
            return;
        }

        await _portProvider.CloseAsync(cancellationToken).ConfigureAwait(false);
        SetStatus(SerialConnectionStatus.Closed, "Cổng đang đóng.");
    }

    public void StartRecording()
    {
        _portErrorDuringRecording = false;
        lock (_sync)
            _recordingChunkTimes.Clear();

        _currentSession ??= new SerialCaptureSession { PortSettings = CloneSettings(Settings) };
        _currentSession.PortSettings = CloneSettings(Settings);
        _currentSession.StartedAt = DateTimeOffset.Now;
        _logWriter.StartSession(_currentSession);
        NotifyStateChanged();
    }

    public void StopRecording() => _logWriter.StopRecording();

    public void DiscardRecording()
    {
        _logWriter.DiscardRecording();
        lock (_sync)
            _recordingChunkTimes.Clear();
        NotifyStateChanged();
    }

    public CaptureEventMarker AddEventMarker(string eventType, string label)
    {
        var elapsed = RecordingStartedAt is null ? 0 : (DateTimeOffset.Now - RecordingStartedAt.Value).TotalMilliseconds;
        return _logWriter.AddEventMarker(eventType, label, elapsed, RecordingTotalBytes, LastChunkNumber);
    }

    public async Task<CaptureSaveResult> SaveLogAsync(string? directoryPath, CancellationToken cancellationToken = default)
    {
        var dir = ResolveDirectory(directoryPath);
        return await _logWriter.SaveToFileAsync(dir, Settings.PortName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CaptureSaveResult> SaveLogAsync(string? directoryPath, CaptureSaveOptions options, CancellationToken cancellationToken = default)
    {
        var dir = ResolveDirectory(directoryPath);
        return await _logWriter.SaveToFileAsync(dir, options, cancellationToken).ConfigureAwait(false);
    }

    public byte[] GetRecordedRawBytes() => _logWriter.GetRecordedRawBytes();

    public void ClearDisplay()
    {
        lock (_sync)
        {
            _displayChunks.Clear();
            _accumulatedBuffer.Clear();
            _eventChunksText = string.Empty;
            _latestHexLine = string.Empty;
            _latestEscapedText = string.Empty;
        }

        NotifyStateChanged();
    }

    public void UpdateSessionMetadata(SerialCaptureSession session)
    {
        if (_currentSession is null)
        {
            _currentSession = session;
        }
        else
        {
            var startedAt = _currentSession.StartedAt;
            _currentSession.PortSettings = session.PortSettings;
            _currentSession.SessionLabel = session.SessionLabel;
            _currentSession.SessionType = session.SessionType;
            _currentSession.ScaleDisplayWeight = session.ScaleDisplayWeight;
            _currentSession.KnownWeightKg = session.KnownWeightKg;
            _currentSession.KnownWeightIsApproximate = session.KnownWeightIsApproximate;
            _currentSession.IsStableSession = session.IsStableSession;
            _currentSession.VehicleCondition = session.VehicleCondition;
            _currentSession.SessionStartNote = session.SessionStartNote;
            _currentSession.AdditionalNotes = session.AdditionalNotes;
            if (startedAt != default)
                _currentSession.StartedAt = startedAt;
            else if (session.StartedAt != default)
                _currentSession.StartedAt = session.StartedAt;
        }

        if (IsRecording)
            _logWriter.WriteSessionNotes(_currentSession);
        NotifyStateChanged();
    }

    public void ApplyPendingUiUpdates()
    {
        var changed = false;
        while (_pendingUiUpdates.TryDequeue(out var update))
        {
            lock (_sync)
            {
                if (!IsDisplayPaused)
                {
                    AppendDisplayChunk(update.Chunk, update.Hex, update.Escaped, update.EventBlock);
                    changed = true;
                }
            }
        }

        if (changed)
            NotifyStateChanged();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _portProvider.DataReceived -= OnProviderDataReceived;
        _portProvider.ReadFailed -= OnProviderReadFailed;
        try
        {
            _portProvider.Dispose();
        }
        catch
        {
            // Best effort.
        }

        _logWriter.Dispose();
    }

    private void OnProviderReadFailed(object? sender, Exception ex)
    {
        if (IsRecording)
            _portErrorDuringRecording = true;

        var mapped = SerialPortExceptionMapper.Map(ex, Settings.PortName);
        SetStatus(mapped.Status, mapped.Message);
    }

    private void OnProviderDataReceived(object? sender, byte[] data)
    {
        if (data.Length == 0)
            return;

        var chunkNumber = Interlocked.Increment(ref _chunkSequence);
        var chunk = new SerialDataChunk
        {
            ChunkNumber = chunkNumber,
            Timestamp = DateTimeOffset.Now,
            RawBytes = data
        };

        var hex = SerialHexFormatter.ToHexString(data);
        var escaped = SerialTextEscaper.Escape(data);
        var eventBlock = FormatEventBlock(chunk, hex, escaped);

        lock (_sync)
        {
            _totalBytes += data.Length;
            _dataReceivedCount++;
            _lastDataAt = chunk.Timestamp;
            UpdateRateWindow(data.Length, chunk.Timestamp);
            _accumulatedBuffer.AddRange(data);
            _latestHexLine = hex;
            _latestEscapedText = escaped;
            if (IsRecording)
                _recordingChunkTimes.Add(chunk.Timestamp);
        }

        if (IsRecording)
            _logWriter.WriteChunk(chunk, hex, escaped);

        RawBytesReceived?.Invoke(this, data);

        _pendingUiUpdates.Enqueue(new PendingUiUpdate(chunk, hex, escaped, eventBlock));

        if (_status is SerialConnectionStatus.Open or SerialConnectionStatus.NoData)
            SetStatus(SerialConnectionStatus.Receiving, "Đang nhận dữ liệu.");
    }

    private void AppendDisplayChunk(SerialDataChunk chunk, string hex, string escaped, string eventBlock)
    {
        _displayChunks.Add(chunk);
        if (_displayChunks.Count > SerialCaptureUiLimits.MaxDisplayChunks)
            _displayChunks.RemoveAt(0);

        _eventChunksText = string.IsNullOrEmpty(_eventChunksText)
            ? eventBlock
            : _eventChunksText + Environment.NewLine + eventBlock;
    }

    private static string FormatEventBlock(SerialDataChunk chunk, string hex, string escaped) =>
        $"[{chunk.Timestamp:HH:mm:ss.fff}]{Environment.NewLine}" +
        $"Bytes: {chunk.ByteCount}{Environment.NewLine}" +
        $"HEX: {hex}{Environment.NewLine}" +
        $"TEXT: {escaped}";

    private async Task MonitorNoDataAsync()
    {
        while (!_disposed && IsPortOpen)
        {
            await Task.Delay(2000).ConfigureAwait(false);
            if (_disposed || !IsPortOpen)
                return;

            if (_lastDataAt is null && _status == SerialConnectionStatus.Open)
                SetStatus(SerialConnectionStatus.NoData, SerialPortErrorMessages.NoDataYet);
        }
    }

    private void UpdateRateWindow(int byteCount, DateTimeOffset at)
    {
        _rateWindowBytes += byteCount;
        _rateWindowStart ??= at;
        var elapsed = (at - _rateWindowStart.Value).TotalSeconds;
        if (elapsed >= 1)
        {
            _bytesPerSecondRecent = _rateWindowBytes / elapsed;
            _rateWindowStart = at;
            _rateWindowBytes = 0;
        }
    }

    private void SetStatus(SerialConnectionStatus status, string? message)
    {
        _status = status;
        _statusMessage = message;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private static string ResolveDirectory(string? directoryPath) =>
        string.IsNullOrWhiteSpace(directoryPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CanXeDeviceTester", "Logs")
            : directoryPath;

    private static SerialPortSettings CloneSettings(SerialPortSettings source) => new()
    {
        PortName = source.PortName,
        BaudRate = source.BaudRate,
        DataBits = source.DataBits,
        Parity = source.Parity,
        StopBits = source.StopBits,
        Handshake = source.Handshake,
        ReadTimeout = source.ReadTimeout,
        TextEncoding = source.TextEncoding
    };

    private sealed record PendingUiUpdate(SerialDataChunk Chunk, string Hex, string Escaped, string EventBlock);
}
