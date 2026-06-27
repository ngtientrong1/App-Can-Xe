namespace CanXe.ScaleProtocol.Core;

public interface IScaleSerialReader : IDisposable
{
    ScaleConnectionState ConnectionState { get; }
    ScaleSerialSettings Settings { get; }
    ScaleParserStatistics ParserStatistics { get; }
    ScaleStabilityState StabilityState { get; }
    ScaleReading? LatestReading { get; }
    DateTimeOffset? LastValidFrameAt { get; }
    bool IsStale { get; }
    string? LastError { get; }

    event EventHandler<ScaleReading>? ValidReadingReceived;
    event EventHandler<ScaleConnectionState>? ConnectionStateChanged;
    event EventHandler? DiagnosticsChanged;

    void UpdateSettings(ScaleSerialSettings settings);
    void ResetSession();
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
