using System.Collections.ObjectModel;
using System.Windows;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CanXe.Desktop.ViewModels;

public sealed partial class ReportViewModel : ObservableObject
{
    private readonly IReportService _reportService;
    private readonly IPrintNotificationService _notificationService;
    private readonly StationSettingsService _stationSettings;

    public ReportViewModel(
        IReportService reportService,
        IPrintNotificationService notificationService,
        StationSettingsService stationSettings)
    {
        _reportService = reportService;
        _notificationService = notificationService;
        _stationSettings = stationSettings;
        FromDate = DateTime.Today.AddDays(-7);
        ToDate = DateTime.Today;
    }

    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string? _customerKeyword;
    [ObservableProperty] private string? _licensePlateKeyword;
    [ObservableProperty] private string? _cargoTypeKeyword;
    [ObservableProperty] private ReportWorkflowFilter _workflowFilter = ReportWorkflowFilter.All;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

    [ObservableProperty] private int _summaryTicketCount;
    [ObservableProperty] private decimal _summaryTotalNetKg;
    [ObservableProperty] private decimal _summaryTotalBillableKg;
    [ObservableProperty] private decimal _summaryTotalAmountVnd;
    [ObservableProperty] private int _summaryMissingPriceCount;
    [ObservableProperty] private int _summaryAwaitingSecondWeighCount;

    public ObservableCollection<ReportRowDto> Rows { get; } = [];

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var result = await _reportService.QueryAsync(BuildFilter());
            ApplyResult(result);
            StatusMessage = $"Đã lọc {result.Summary.TicketCount} phiếu.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FromDate = DateTime.Today.AddDays(-7);
        ToDate = DateTime.Today;
        CustomerKeyword = null;
        LicensePlateKeyword = null;
        CargoTypeKeyword = null;
        WorkflowFilter = ReportWorkflowFilter.All;
        await ApplyFilterAsync();
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (IsBusy)
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"BaoCaoPhieuCan_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            DefaultExt = ".xlsx"
        };

        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        try
        {
            var station = await _stationSettings.GetStationAsync();
            var result = await _reportService.ExportExcelAsync(BuildFilter(), dialog.FileName, station.StationName);

            if (!result.Success)
            {
                if (string.Equals(result.ErrorMessage, "cancelled", StringComparison.OrdinalIgnoreCase))
                    return;

                MessageBox.Show(
                    result.ErrorMessage ?? "Không thể xuất file Excel.",
                    "Lỗi xuất Excel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            await _notificationService.ShowExportSuccessToastAsync(result.FilePath);
            StatusMessage = "Đã xuất file Excel.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi xuất Excel", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ReportFilter BuildFilter() =>
        new()
        {
            FromDate = FromDate.HasValue ? new DateTimeOffset(FromDate.Value.Date) : null,
            ToDate = ToDate.HasValue ? new DateTimeOffset(ToDate.Value.Date) : null,
            CustomerKeyword = CustomerKeyword,
            LicensePlateKeyword = LicensePlateKeyword,
            CargoTypeKeyword = CargoTypeKeyword,
            State = WorkflowFilter
        };

    private void ApplyResult(ReportResult result)
    {
        Rows.Clear();
        foreach (var row in result.Rows)
            Rows.Add(row);

        SummaryTicketCount = result.Summary.TicketCount;
        SummaryTotalNetKg = result.Summary.TotalNetWeightKg;
        SummaryTotalBillableKg = result.Summary.TotalBillableWeightKg;
        SummaryTotalAmountVnd = result.Summary.TotalAmountVnd;
        SummaryMissingPriceCount = result.Summary.MissingPriceCount;
        SummaryAwaitingSecondWeighCount = result.Summary.AwaitingSecondWeighCount;
    }

    public async Task InitializeAsync() => await ApplyFilterAsync();
}
