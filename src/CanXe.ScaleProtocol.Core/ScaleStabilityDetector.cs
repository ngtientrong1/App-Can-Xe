namespace CanXe.ScaleProtocol.Core;

public sealed class ScaleStabilityOptions
{
    public int RequiredMatchingFrames { get; init; } = 8;
    public long MaximumWeightSpreadKg { get; init; } = 0;
    public int MaximumGapMilliseconds { get; init; } = 1000;
}

public sealed class ScaleStabilityState
{
    public bool IsStable { get; init; }
    public int ConsecutiveMatchingFrames { get; init; }
    public long? CurrentWeightKg { get; init; }
}

public sealed class ScaleStabilityDetector
{
    private readonly ScaleStabilityOptions _options;
    private long? _currentWeightKg;
    private int _consecutiveMatching;
    private DateTimeOffset? _lastValidFrameAt;
    private bool _isStable;

    public ScaleStabilityDetector(ScaleStabilityOptions? options = null) =>
        _options = options ?? new ScaleStabilityOptions();

    public ScaleStabilityState CurrentState => new()
    {
        IsStable = _isStable,
        ConsecutiveMatchingFrames = _consecutiveMatching,
        CurrentWeightKg = _currentWeightKg
    };

    public ScaleStabilityState NotifyValidReading(ScaleProtocolFrame frame, DateTimeOffset receivedAt)
    {
        var magnitude = int.Parse(frame.WeightDigits);
        var weightKg = frame.Sign == '-' ? -magnitude : magnitude;

        if (_lastValidFrameAt.HasValue)
        {
            var gapMs = (receivedAt - _lastValidFrameAt.Value).TotalMilliseconds;
            if (gapMs > _options.MaximumGapMilliseconds)
                ResetInternal();
        }

        if (_currentWeightKg is null || weightKg != _currentWeightKg)
        {
            _currentWeightKg = weightKg;
            _consecutiveMatching = 1;
            _isStable = _consecutiveMatching >= _options.RequiredMatchingFrames;
        }
        else
        {
            _consecutiveMatching++;
            _isStable = _consecutiveMatching >= _options.RequiredMatchingFrames;
        }

        _lastValidFrameAt = receivedAt;
        return CurrentState;
    }

    public void NotifyInvalidFrame(DateTimeOffset receivedAt)
    {
        _ = receivedAt;
        ResetInternal();
    }

    public void Reset()
    {
        ResetInternal();
        _lastValidFrameAt = null;
    }

    private void ResetInternal()
    {
        _currentWeightKg = null;
        _consecutiveMatching = 0;
        _isStable = false;
    }
}
