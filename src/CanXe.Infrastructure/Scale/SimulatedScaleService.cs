using CanXe.Application.Interfaces;

namespace CanXe.Infrastructure.Scale;

public sealed class SimulatedScaleService : IScaleService
{
    private readonly Random _random = new();
    private CancellationTokenSource? _timerCts;
    private Task? _timerTask;
    private decimal _currentWeightKg;
    private bool _manualMode;

    public event EventHandler<decimal>? WeightChanged;

    public bool IsManualMode => _manualMode;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timerTask = RunRandomLoopAsync(_timerCts.Token);
        return Task.CompletedTask;
    }

    public Task<decimal> GetCurrentWeightAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_currentWeightKg);

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_timerCts is not null)
        {
            await _timerCts.CancelAsync();
            if (_timerTask is not null)
            {
                try { await _timerTask; }
                catch (OperationCanceledException) { }
            }
            _timerCts.Dispose();
            _timerCts = null;
        }
    }

    public void SetManualMode(bool enabled)
    {
        _manualMode = enabled;
        if (enabled)
            PublishWeight(_currentWeightKg);
    }

    public void SetManualWeightKg(decimal weightKg)
    {
        _currentWeightKg = Math.Round(weightKg, 0, MidpointRounding.AwayFromZero);
        PublishWeight(_currentWeightKg);
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task RunRandomLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_manualMode)
            {
                var next = _random.Next(8000, 25001);
                PublishWeight(next);
            }

            try
            {
                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void PublishWeight(decimal weightKg)
    {
        _currentWeightKg = weightKg;
        WeightChanged?.Invoke(this, weightKg);
    }
}
