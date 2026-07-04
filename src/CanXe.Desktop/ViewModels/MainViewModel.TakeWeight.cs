using System.Diagnostics;

using CanXe.Application.Models;

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



    [ObservableProperty] private bool _isTakingWeight;

    [ObservableProperty] private TakeWeightAction _activeTakeWeightAction = TakeWeightAction.None;

    [ObservableProperty] private string? _takeWeightStatusText;



    public string? LastTakeWeightOperationId => _lastTakeWeightOperationId;

    public long LastTakeWeightElapsedMs => _lastTakeWeightElapsedMs;



    partial void OnIsTakingWeightChanged(bool value)

    {

        UpdateButtonStates();

        UpdateButtonLabels();

        OnPropertyChanged(nameof(LastTakeWeightOperationId));

        OnPropertyChanged(nameof(LastTakeWeightElapsedMs));

    }



    [RelayCommand]

    private async Task CaptureWeight1Async() => await CaptureWeightAsync(1, TakeWeightAction.First);



    [RelayCommand]

    private async Task CaptureWeight2Async() => await CaptureWeightAsync(2, TakeWeightAction.Second);



    private async Task CaptureWeightAsync(int sequence, TakeWeightAction action)

    {

        if (!await _takeWeightGate.WaitAsync(0).ConfigureAwait(true))

            return;



        var operationId = OperatorActionLogger.CreateOperationId(sequence == 1 ? "TW1" : "TW2");

        _lastTakeWeightOperationId = operationId;

        var sw = Stopwatch.StartNew();



        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>

        {

            IsTakingWeight = true;

            ActiveTakeWeightAction = action;

            TakeWeightStatusText = sequence == 1 && _draft.DraftWeight1.HasValue

                ? "ĐANG CẬP NHẬT..."

                : "ĐANG LẤY CÂN...";

            StatusMessage = TakeWeightStatusText;

        });

        var clickFeedbackMs = sw.ElapsedMilliseconds;



        var scaleReading = _hardwareScale?.LatestReading;

        OperatorActionLogger.RegisterTakeWeightContext(

            operationId,

            action.ToString(),

            scaleReading);



        long validationMs = 0;

        long readingMs = 0;

        long updateMs = 0;



        try

        {

            var validationSw = Stopwatch.StartNew();

            if (IsEditingExistingTicket || FormMode == TicketFormMode.Viewing)

            {

                StatusMessage = FormMode == TicketFormMode.Viewing

                    ? "Không thể lấy cân khi đang xem phiếu."

                    : "Không thể lấy cân khi đang chỉnh sửa phiếu.";

                return;

            }



            if (TryBlockHardwareConnection(out var blockReason))

            {

                StatusMessage = blockReason;

                return;

            }



            validationMs = validationSw.ElapsedMilliseconds;



            _draft.DeveloperWeight1OverrideEnabled = DeveloperWeight1OverrideEnabled;



            var readingSw = Stopwatch.StartNew();

            var result = await _weighTicketService.CaptureWeightAsync(_draft, sequence).ConfigureAwait(true);

            readingMs = readingSw.ElapsedMilliseconds;



            if (!result.Success)

            {

                StatusMessage = result.ErrorMessage ?? "KHÔNG THỂ LẤY CÂN";

                return;

            }



            var updateSw = Stopwatch.StartNew();

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>

            {

                UpdateDisplaysFromDraft();

                UpdateButtonStates();

                UpdateButtonLabels();

                TakeWeightStatusText = $"ĐÃ LẤY {result.WeightKg:N0} kg";

                StatusMessage = result.IsUpdate

                    ? $"ĐÃ CẬP NHẬT CÂN LẦN {sequence}: {result.WeightKg:N0} kg"

                    : $"ĐÃ LẤY CÂN LẦN {sequence}: {result.WeightKg:N0} kg";

            });

            updateMs = updateSw.ElapsedMilliseconds;



            sw.Stop();

            _lastTakeWeightElapsedMs = sw.ElapsedMilliseconds;

            OperatorActionLogger.WritePerformance("TakeWeight",

                $"total={sw.ElapsedMilliseconds}ms click={clickFeedbackMs}ms validation={validationMs}ms reading={readingMs}ms update={updateMs}ms");

        }

        catch (Exception ex)

        {

            sw.Stop();

            _lastTakeWeightElapsedMs = sw.ElapsedMilliseconds;

            StatusMessage = "KHÔNG THỂ LẤY CÂN";

            OperatorActionLogger.Write(operationId, action.ToString(), "CommandException", ex.Message, sw.ElapsedMilliseconds, ex);

        }

        finally

        {

            _takeWeightGate.Release();

            IsTakingWeight = false;

            ActiveTakeWeightAction = TakeWeightAction.None;

            if (TakeWeightStatusText is "ĐANG LẤY CÂN..." or "ĐANG CẬP NHẬT...")

                TakeWeightStatusText = null;

        }

    }

}



public enum TakeWeightAction

{

    None,

    First,

    Second

}


