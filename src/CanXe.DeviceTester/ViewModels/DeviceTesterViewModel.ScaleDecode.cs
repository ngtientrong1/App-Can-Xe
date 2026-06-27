using System.Globalization;
using System.Text;
using CanXe.ScaleProtocol.Core;

namespace CanXe.DeviceTester.ViewModels;

public partial class DeviceTesterViewModel
{
    private readonly ScaleFrameParser _scaleFrameParser = new();
    private readonly ScaleStabilityDetector _scaleStabilityDetector = new();
    private string _decodedFrameText = "—";
    private string _decodedWeightText = "—";
    private string _decodedProtocolCode = "—";
    private string _decodedChecksumReceived = "—";
    private string _decodedChecksumExpected = "—";
    private string _decodedValidityText = "—";
    private string _decodedStabilityText = "—";
    private string _decodedStatsText = "—";
    private string? _decodedLastValidFrameTime;

    public string DecodedFrameText { get => _decodedFrameText; private set => SetField(ref _decodedFrameText, value); }
    public string DecodedWeightText { get => _decodedWeightText; private set => SetField(ref _decodedWeightText, value); }
    public string DecodedProtocolCode { get => _decodedProtocolCode; private set => SetField(ref _decodedProtocolCode, value); }
    public string DecodedChecksumReceived { get => _decodedChecksumReceived; private set => SetField(ref _decodedChecksumReceived, value); }
    public string DecodedChecksumExpected { get => _decodedChecksumExpected; private set => SetField(ref _decodedChecksumExpected, value); }
    public string DecodedValidityText { get => _decodedValidityText; private set => SetField(ref _decodedValidityText, value); }
    public string DecodedStabilityText { get => _decodedStabilityText; private set => SetField(ref _decodedStabilityText, value); }
    public string DecodedStatsText { get => _decodedStatsText; private set => SetField(ref _decodedStatsText, value); }
    public string? DecodedLastValidFrameTime { get => _decodedLastValidFrameTime; private set => SetField(ref _decodedLastValidFrameTime, value); }

    partial void InitializeScaleDecode()
    {
        _capture.RawBytesReceived += (_, data) =>
            System.Windows.Application.Current?.Dispatcher.Invoke(() => ProcessScaleBytes(data));
    }

    private void ProcessScaleBytes(byte[] data)
    {
        var readings = _scaleFrameParser.Append(data, DateTimeOffset.Now, _scaleStabilityDetector);
        if (readings.Count == 0)
        {
            UpdateDecodedStatsOnly();
            return;
        }

        var latest = readings[^1];
        DecodedFrameText = Encoding.ASCII.GetString(latest.RawFrame);
        DecodedWeightText = $"{latest.WeightKg} kg";
        DecodedProtocolCode = latest.ProtocolCode;
        DecodedChecksumReceived = latest.Checksum.ToString();
        DecodedChecksumExpected = latest.ExpectedChecksum.ToString();
        DecodedValidityText = latest.IsChecksumValid ? "Valid" : "Invalid";
        DecodedStabilityText = latest.IsStable ? "Stable" : "Unstable";
        DecodedLastValidFrameTime = latest.ReceivedAt.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture);
        UpdateDecodedStatsOnly();
    }

    private void UpdateDecodedStatsOnly()
    {
        var stats = _scaleFrameParser.Statistics;
        var stability = _scaleStabilityDetector.CurrentState;
        DecodedStatsText =
            $"Consecutive valid frames: {stability.ConsecutiveMatchingFrames}{Environment.NewLine}" +
            $"Frames valid: {stats.ValidFrames}{Environment.NewLine}" +
            $"Frames invalid: {stats.InvalidFrames}{Environment.NewLine}" +
            $"Discarded bytes: {stats.DiscardedBytes}{Environment.NewLine}" +
            $"Buffered bytes: {_scaleFrameParser.BufferedByteCount}";
    }

    partial void ResetScaleDecode()
    {
        _scaleFrameParser.ResetBuffer();
        _scaleStabilityDetector.Reset();
        DecodedFrameText = "—";
        DecodedWeightText = "—";
        DecodedProtocolCode = "—";
        DecodedChecksumReceived = "—";
        DecodedChecksumExpected = "—";
        DecodedValidityText = "—";
        DecodedStabilityText = "—";
        DecodedLastValidFrameTime = null;
        UpdateDecodedStatsOnly();
    }
}
