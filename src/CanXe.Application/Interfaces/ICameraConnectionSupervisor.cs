using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface ICameraConnectionSupervisor : IAsyncDisposable
{
    CameraConnectionState State { get; }
    bool IsUserDisconnected { get; }
    CameraHealthSnapshot GetHealthSnapshot();

    event EventHandler<CameraConnectionState>? StateChanged;
    event EventHandler<CameraHealthSnapshot>? HealthChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task RequestConnectAsync(CameraConnectRequestSource source, CancellationToken cancellationToken = default);
    Task RequestDisconnectByUserAsync(CancellationToken cancellationToken = default);
    Task RequestReconnectAsync(string reason, CancellationToken cancellationToken = default);
}
