namespace CanXe.ScaleProtocol.Core;

public sealed class ScaleStabilityOptions
{
    public int HardwareStableConfirmFrames { get; init; } = 2;
    public int SoftwareRequiredMatchingFrames { get; init; } = 3;
    public int SoftwareSampleWindowFrames { get; init; } = 6;
    public int SoftwareStableRequiredDurationMs { get; init; } = 400;
    public long ScaleDivisionKg { get; init; } = 20;
    public int MaximumGapMilliseconds { get; init; } = 1000;
}

public sealed class ScaleStabilityState
{
    public bool IsStable { get; init; }
    public ScaleStableSource StableSource { get; init; }
    public int ConsecutiveMatchingFrames { get; init; }
    public long? CurrentWeightKg { get; init; }
    public bool? RawStableFlag { get; init; }
}

public sealed class ScaleStabilityDetector
{
    private readonly ScaleStabilityOptions _options;
    private readonly Queue<(long WeightKg, DateTimeOffset At)> _softwareWindow = new();
    private long? _currentWeightKg;
    private long? _lastHardwareWeightKg;
    private int _consecutiveHardwareStable;
    private int _consecutiveSoftwareMatching;
    private DateTimeOffset? _lastValidFrameAt;
    private bool _isStable;
    private ScaleStableSource _stableSource = ScaleStableSource.Unknown;
    private bool? _rawStableFlag;

    public ScaleStabilityDetector(ScaleStabilityOptions? options = null) =>
        _options = options ?? new ScaleStabilityOptions();

    public ScaleStabilityState CurrentState => new()
    {
        IsStable = _isStable,
        StableSource = _stableSource,
        ConsecutiveMatchingFrames = Math.Max(_consecutiveHardwareStable, _consecutiveSoftwareMatching),
        CurrentWeightKg = _currentWeightKg,
        RawStableFlag = _rawStableFlag
    };

    public ScaleStabilityState NotifyValidReading(ScaleProtocolFrame frame, DateTimeOffset receivedAt)
    {
        var magnitude = int.Parse(frame.WeightDigits);
        var weightKg = frame.Sign == '-' ? -magnitude : magnitude;
        var status = ScaleProtocolStatus.Interpret(frame.ProtocolCode);

        if (_lastValidFrameAt.HasValue)
        {
            var gapMs = (receivedAt - _lastValidFrameAt.Value).TotalMilliseconds;
            if (gapMs > _options.MaximumGapMilliseconds)
                ResetInternal();
        }

        _rawStableFlag = status.HardwareStable;
        _currentWeightKg = weightKg;

        if (status.HardwareStable == true)
        {
            if (_lastHardwareWeightKg is not null && !WeightsMatch(weightKg, _lastHardwareWeightKg.Value))
                _consecutiveHardwareStable = 1;
            else
                _consecutiveHardwareStable++;

            _lastHardwareWeightKg = weightKg;

            if (_consecutiveHardwareStable >= _options.HardwareStableConfirmFrames)
            {
                _isStable = true;
                _stableSource = ScaleStableSource.HardwareFlag;
            }
            else
            {
                _isStable = false;
                _stableSource = ScaleStableSource.HardwareFlag;
            }
        }
        else if (status.HardwareStable == false)
        {
            _consecutiveHardwareStable = 0;
            _lastHardwareWeightKg = weightKg;
            _isStable = false;
            _stableSource = ScaleStableSource.HardwareFlag;
            ResetSoftwareWindow();
        }
        else
        {
            UpdateSoftwareWindow(weightKg, receivedAt);
        }

        _lastValidFrameAt = receivedAt;
        return CurrentState;
    }

    public void NotifyInvalidFrame(DateTimeOffset receivedAt)
    {
        _ = receivedAt;
        ResetInternal();
        _lastValidFrameAt = null;
    }

    public void Reset()
    {
        ResetInternal();
        _lastValidFrameAt = null;
    }

    private void UpdateSoftwareWindow(long weightKg, DateTimeOffset receivedAt)
    {
        _stableSource = ScaleStableSource.SoftwareWindow;
        _consecutiveHardwareStable = 0;

        if (_currentWeightKg is null || WeightsMatch(weightKg, _currentWeightKg.Value))
            _consecutiveSoftwareMatching++;
        else
            _consecutiveSoftwareMatching = 1;

        _softwareWindow.Enqueue((weightKg, receivedAt));
        while (_softwareWindow.Count > _options.SoftwareSampleWindowFrames)
            _softwareWindow.Dequeue();

        var span = _softwareWindow.Count >= 2
            ? receivedAt - _softwareWindow.Peek().At
            : TimeSpan.Zero;

        var spread = _softwareWindow.Count == 0
            ? 0
            : _softwareWindow.Max(x => x.WeightKg) - _softwareWindow.Min(x => x.WeightKg);

        var divisionOk = spread <= _options.ScaleDivisionKg;
        var durationOk = span.TotalMilliseconds >= _options.SoftwareStableRequiredDurationMs;
        var framesOk = _consecutiveSoftwareMatching >= _options.SoftwareRequiredMatchingFrames
                         && _softwareWindow.Count >= _options.SoftwareRequiredMatchingFrames;

        _isStable = divisionOk && durationOk && framesOk;
    }

    private bool WeightsMatch(long a, long b) =>
        Math.Abs(a - b) <= _options.ScaleDivisionKg;

    private void ResetSoftwareWindow()
    {
        _softwareWindow.Clear();
        _consecutiveSoftwareMatching = 0;
    }

    private void ResetInternal()
    {
        _currentWeightKg = null;
        _lastHardwareWeightKg = null;
        _consecutiveHardwareStable = 0;
        _consecutiveSoftwareMatching = 0;
        _isStable = false;
        _stableSource = ScaleStableSource.Unknown;
        _rawStableFlag = null;
        ResetSoftwareWindow();
    }
}
