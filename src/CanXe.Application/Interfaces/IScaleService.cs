namespace CanXe.Application.Interfaces;

public interface IScaleService : IAsyncDisposable
{
    event EventHandler<decimal>? WeightChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetCurrentWeightAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    bool IsManualMode { get; }
    void SetManualMode(bool enabled);
    void SetManualWeightKg(decimal weightKg);
}
