using CanXe.Domain.Models;
using CanXe.ScaleProtocol.Core;

namespace CanXe.Application.Interfaces;

public interface IHardwareScaleDiagnostics
{
    ScaleInputMode InputMode { get; }
    ScaleConnectionState ConnectionState { get; }
    bool IsConnected { get; }
    bool IsStale { get; }
    bool IsStable { get; }
    DateTimeOffset? LastValidFrameAt { get; }
    string? LastChecksumError { get; }
    ScaleReading? LatestReading { get; }
    int ValidFrameCount { get; }
    int InvalidFrameCount { get; }
    long DiscardedBytes { get; }
    ScaleSerialSettings HardwareSettings { get; }

    event EventHandler? HardwareDiagnosticsChanged;

    Task SetInputModeAsync(ScaleInputMode mode, CancellationToken cancellationToken = default);
    void SetInputMode(ScaleInputMode mode);
    Task PrepareAndConnectHardwareAsync(ScaleSerialSettings settings, CancellationToken cancellationToken = default);
    void UpdateHardwareSettings(ScaleSerialSettings settings);
    Task ConnectHardwareAsync(CancellationToken cancellationToken = default);
    Task DisconnectHardwareAsync(CancellationToken cancellationToken = default);
    bool CanCaptureWeight();
    string? GetHardwareCaptureBlockReason();
    string? GetLatestFrameDisplay();
}
