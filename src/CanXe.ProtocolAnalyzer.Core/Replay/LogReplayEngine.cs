using CanXe.ProtocolAnalyzer.Core.Models;

namespace CanXe.ProtocolAnalyzer.Core.Replay;

public sealed class LogReplayEngine : IDisposable
{
    private readonly SerialLogSession _session;
    private readonly ContinuousByteStream _stream;
    private readonly IReadOnlyList<SerialLogChunk> _chunks;
    private CancellationTokenSource? _cts;
    private Task? _replayTask;
    private int _chunkIndex;
    private int _byteOffset;

    public LogReplayEngine(SerialLogSession session)
    {
        _session = session;
        _stream = session.Stream ?? throw new InvalidOperationException("Session chưa có continuous stream.");
        _chunks = _stream.SourceChunks;
    }

    public double SpeedMultiplier { get; set; } = 1.0;
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public int CurrentChunkIndex => _chunkIndex;
    public int CurrentByteOffset => _byteOffset;
    public DateTimeOffset? CurrentTimestamp => _chunkIndex >= 0 && _chunkIndex < _chunks.Count
        ? _chunks[_chunkIndex].Timestamp
        : null;

    public event EventHandler<LogReplayState>? StateChanged;

    public async Task PlayAsync(bool instant = false, CancellationToken cancellationToken = default)
    {
        StopInternal();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsPlaying = true;
        IsPaused = false;

        if (instant)
        {
            _chunkIndex = Math.Max(0, _chunks.Count - 1);
            _byteOffset = _stream.Bytes.Length;
            RaiseState();
            IsPlaying = false;
            return;
        }

        _replayTask = Task.Run(async () =>
        {
            for (; _chunkIndex < _chunks.Count; _chunkIndex++)
            {
                _cts.Token.ThrowIfCancellationRequested();
                while (IsPaused)
                    await Task.Delay(50, _cts.Token).ConfigureAwait(false);

                var chunk = _chunks[_chunkIndex];
                _byteOffset = FindStreamOffset(_chunkIndex);
                RaiseState();

                if (_chunkIndex + 1 < _chunks.Count && SpeedMultiplier > 0)
                {
                    var delay = _chunks[_chunkIndex + 1].Timestamp - chunk.Timestamp;
                    var ms = Math.Max(1, (int)(delay.TotalMilliseconds / SpeedMultiplier));
                    await Task.Delay(ms, _cts.Token).ConfigureAwait(false);
                }
            }

            IsPlaying = false;
            RaiseState();
        }, _cts.Token);

        await _replayTask.ConfigureAwait(false);
    }

    public void Pause() => IsPaused = true;

    public void Resume() => IsPaused = false;

    public void Stop()
    {
        StopInternal();
        _chunkIndex = 0;
        _byteOffset = 0;
        RaiseState();
    }

    private void StopInternal()
    {
        _cts?.Cancel();
        _replayTask = null;
        IsPlaying = false;
        IsPaused = false;
    }

    private int FindStreamOffset(int chunkIndex)
    {
        if (chunkIndex < 0 || chunkIndex >= _chunks.Count)
            return _stream.Bytes.Length;

        var chunkNumber = _chunks[chunkIndex].ChunkNumber;
        var mapping = _stream.Mappings.LastOrDefault(m => m.ChunkNumber == chunkNumber);
        return mapping?.StreamOffset ?? 0;
    }

    private void RaiseState()
    {
        var chunk = _chunkIndex >= 0 && _chunkIndex < _chunks.Count ? _chunks[_chunkIndex] : null;
        StateChanged?.Invoke(this, new LogReplayState
        {
            ChunkIndex = _chunkIndex,
            ChunkNumber = chunk?.ChunkNumber ?? -1,
            Timestamp = chunk?.Timestamp,
            ByteOffset = _byteOffset,
            CurrentHex = chunk is null ? string.Empty : FormatHex(chunk.Bytes),
            AccumulatedHex = FormatHex(_stream.Bytes.Take(_byteOffset + (chunk?.Bytes.Length ?? 0)).ToArray())
        });
    }

    private static string FormatHex(byte[] bytes) =>
        bytes.Length == 0 ? string.Empty : string.Join(' ', bytes.Select(b => b.ToString("X2")));

    public void Dispose() => StopInternal();
}

public sealed class LogReplayState
{
    public int ChunkIndex { get; init; }
    public int ChunkNumber { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public int ByteOffset { get; init; }
    public string CurrentHex { get; init; } = string.Empty;
    public string AccumulatedHex { get; init; } = string.Empty;
}
