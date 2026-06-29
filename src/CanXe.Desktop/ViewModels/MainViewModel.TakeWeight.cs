using System.Diagnostics;
using CanXe.Application.Models;
using CanXe.Domain.Models;
using CanXe.Infrastructure.Logging;
using CanXe.ScaleProtocol.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private readonly SemaphoreSlim _takeWeightGate = new(1, 1);
    private string? _lastTakeWeightOperationId;
    private long _lastTakeWeightElapsedMs;
    private string? _lastSnapshotResult;

    [ObservableProperty] private bool _isTakingWeight;
    [ObservableProperty] private bool _isSavingWeightSnapshot;
    [ObservableProperty] private TakeWeightAction _activeTakeWeightAction = TakeWeightAction.None;
    [ObservableProperty] private string? _takeWeightStatusText;

    public string? LastTakeWeightOperationId => _lastTakeWeightOperationId;
    public long LastTakeWeightElapsedMs => _lastTakeWeightElapsedMs;
    public string? LastSnapshotResult => _lastSnapshotResult;

    partial void OnIsTakingWeightChanged(bool value)
    {
        UpdateButtonStates();
        UpdateButtonLabels();
        OnPropertyChanged(nameof(LastTakeWeightOperationId));
        OnPropertyChanged(nameof(LastTakeWeightElapsedMs));
        RefreshOperatorCameraHealth();
    }

    private void WireOperatorActionCorrelation()
    {
        _cameraSupervisor.StateChanged += (_, state) =>
        {
            OperatorActionLogger.TryLogCameraReconnectCorrelation(
                state,
                _cameraSupervisor.GetHealthSnapshot().LastDisconnectReason ?? "StateChanged",
                _cameraStream.LastDecodedFrameAt ?? _cameraStream.LastCachedFrameAt,
                _cameraStream.LastFrameAge,
                _cameraStream.LastUiRenderedFrameAt,
                PreviewUpdatePending,
                _isSavingWeightSnapshot);
            RunOnUiThread(RefreshOperatorCameraHealth);
        };

        _cameraSupervisor.HealthChanged += (_, _) => RunOnUiThread(RefreshOperatorCameraHealth);
    }

    private void RefreshOperatorCameraHealth()
    {
        var health = _cameraSupervisor.GetHealthSnapshot();
        Settings.RefreshCameraHealth(health, BuildExtendedHealthText(health));
    }

    private string BuildExtendedHealthText(CameraHealthSnapshot health)
    {
        var lines = new List<string> { health.ToDisplayText() };
        lines.Add($"Latest cached frame age: {_latestCameraFrameProvider.LatestFrameAge?.TotalSeconds.ToString("F2") ?? "—"}s");
        lines.Add($"Last decoded frame age: {FormatAge(_cameraStream.LastDecodedFrameAt)}");
        lines.Add($"Last UI render age: {FormatAge(_cameraStream.LastUiRenderedFrameAt)}");
        lines.Add($"Preview received/rendered/dropped: {PreviewFramesReceived}/{PreviewFramesRendered}/{PreviewFramesDropped}");
        lines.Add($"Preview pending: {PreviewUpdatePending}");
        lines.Add($"Last snapshot result: {_lastSnapshotResult ?? "—"}");
        lines.Add($"Last TakeWeight elapsed: {_lastTakeWeightElapsedMs} ms");
        lines.Add($"Last TakeWeight operation ID: {_lastTakeWeightOperationId ?? "—"}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatAge(DateTimeOffset? at) =>
        at is null ? "—" : $"{(DateTimeOffset.UtcNow - at.Value).TotalSeconds:F2}s";

    [RelayCommand]
    private async Task CaptureWeight1Async() => await CaptureWeightAsync(1, TakeWeightAction.First);

    [RelayCommand]
    private async Task CaptureWeight2Async() => await CaptureWeightAsync(2, TakeWeightAction.Second);

    private async Task CaptureWeightAsync(int sequence, TakeWeightAction action)
    {
        if (!await _takeWeightGate.WaitAsync(0).ConfigureAwait(false))
            return;

        var operationId = OperatorActionLogger.CreateOperationId(sequence == 1 ? "TW1" : "TW2");
        _lastTakeWeightOperationId = operationId;
        var sw = Stopwatch.StartNew();
        var clickedAt = DateTimeOffset.UtcNow;
        var gateReleased = false;

        IsTakingWeight = true;
        ActiveTakeWeightAction = action;
        TakeWeightStatusText = sequence == 1 && _draft.DraftWeight1.HasValue
            ? "ĐANG CẬP NHẬT..."
            : "ĐANG LẤY CÂN...";
        StatusMessage = TakeWeightStatusText;

        var scaleReading = _hardwareScale?.LatestReading;
        var health = _cameraSupervisor.GetHealthSnapshot();
        OperatorActionLogger.RegisterTakeWeightContext(
            operationId,
            action.ToString(),
            _cameraSupervisor.State,
            scaleReading,
            _cameraStream.LastDecodedFrameAt ?? _cameraStream.LastCachedFrameAt,
            _cameraStream.LastFrameAge,
            _cameraStream.LastUiRenderedFrameAt,
            health.FfmpegPid,
            health.ActiveSessionId,
            health.FramesDecoded,
            PreviewUpdatePending);

        OperatorActionLogger.Write(
            operationId,
            action.ToString(),
            "ButtonClicked",
            $"CameraState={_cameraSupervisor.State}; LastDecodedAge={FormatAge(_cameraStream.LastDecodedFrameAt)}; FfmpegPid={health.FfmpegPid}");

        try
        {
            if (IsEditingExistingTicket)
            {
                StatusMessage = "Không thể lấy cân khi đang chỉnh sửa phiếu.";
                return;
            }

            if (TryBlockHardwareConnection(out var blockReason))
            {
                StatusMessage = blockReason;
                return;
            }

            _draft.DeveloperWeight1OverrideEnabled = DeveloperWeight1OverrideEnabled;
            OperatorActionLogger.Write(operationId, action.ToString(), "CommandStarted");

            var result = await _weighTicketService.CaptureWeightAsync(_draft, sequence).ConfigureAwait(false);
            var weightCapturedAt = DateTimeOffset.UtcNow;
            sw.Stop();
            _lastTakeWeightElapsedMs = sw.ElapsedMilliseconds;

            if (!result.Success)
            {
                StatusMessage = result.ErrorMessage ?? "KHÔNG THỂ LẤY CÂN";
                _lastSnapshotResult = "Weight capture failed";
                OperatorActionLogger.Write(
                    operationId,
                    action.ToString(),
                    "CommandFailed",
                    result.ErrorMessage,
                    sw.ElapsedMilliseconds);
                return;
            }

            OperatorActionLogger.Write(
                operationId,
                action.ToString(),
                "WeightCaptured",
                $"WeightKg={result.WeightKg}; IsUpdate={result.IsUpdate}",
                sw.ElapsedMilliseconds);

            var uiUpdatedAt = DateTimeOffset.UtcNow;
            RunOnUiThread(() =>
            {
                UpdateDisplaysFromDraft();
                UpdateButtonStates();
                UpdateButtonLabels();
                TakeWeightStatusText = $"ĐÃ LẤY {result.WeightKg:N0} kg";
                StatusMessage = result.IsUpdate
                    ? $"ĐÃ CẬP NHẬT CÂN LẦN {sequence}: {result.WeightKg:N0} kg"
                    : $"ĐÃ LẤY CÂN LẦN {sequence}: {result.WeightKg:N0} kg";
            });

            OperatorActionLogger.WriteTakeWeightUiUpdated(
                operationId,
                action.ToString(),
                clickedAt,
                weightCapturedAt,
                uiUpdatedAt,
                scaleReading,
                sw.ElapsedMilliseconds);

            IsTakingWeight = false;
            ActiveTakeWeightAction = TakeWeightAction.None;
            IsSavingWeightSnapshot = true;
            TakeWeightStatusText = "ĐANG LƯU ẢNH...";
            _takeWeightGate.Release();
            gateReleased = true;

            _lastSnapshotResult = "Snapshot scheduled";
            OperatorActionLogger.Write(
                operationId,
                action.ToString(),
                "SnapshotScheduled",
                $"CameraState={_cameraSupervisor.State}; GateReleased=true",
                sw.ElapsedMilliseconds);

            await Task.Delay(600).ConfigureAwait(false);
            RunOnUiThread(() =>
            {
                if (TakeWeightStatusText == "ĐANG LƯU ẢNH...")
                    TakeWeightStatusText = null;
            });

            OperatorActionLogger.Write(
                operationId,
                action.ToString(),
                "CommandCompleted",
                $"CameraState={_cameraSupervisor.State}; TotalElapsed={sw.ElapsedMilliseconds}ms",
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _lastTakeWeightElapsedMs = sw.ElapsedMilliseconds;
            StatusMessage = "KHÔNG THỂ LẤY CÂN";
            OperatorActionLogger.Write(
                operationId,
                action.ToString(),
                "CommandException",
                ex.Message,
                sw.ElapsedMilliseconds,
                ex);
        }
        finally
        {
            if (!gateReleased)
                _takeWeightGate.Release();

            IsTakingWeight = false;
            ActiveTakeWeightAction = TakeWeightAction.None;
            IsSavingWeightSnapshot = false;
            if (TakeWeightStatusText is "ĐANG LẤY CÂN..." or "ĐANG CẬP NHẬT...")
                TakeWeightStatusText = null;
            RefreshOperatorCameraHealth();
        }
    }
}

public enum TakeWeightAction
{
    None,
    First,
    Second
}
