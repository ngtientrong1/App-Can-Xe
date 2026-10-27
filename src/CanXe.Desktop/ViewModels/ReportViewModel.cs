using System.Collections.ObjectModel;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Controls;
using CanXe.Desktop.Services;
using CanXe.Desktop.Views.Reports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CanXe.Desktop.ViewModels;

public sealed partial class ReportViewModel : ObservableObject
{
    private readonly IReportService _reportService;
    private readonly IPrintNotificationService _notificationService;
    private readonly StationSettingsService _stationSettings;
    private readonly FastEntrySearchService _fastEntrySearch;
    private readonly IDebtExportService _debtExport;

    public ReportViewModel(
        IReportService reportService,
        IPrintNotificationService notificationService,
        StationSettingsService stationSettings,
        FastEntrySearchService fastEntrySearch,
        IDebtExportService debtExport)
    {
        _reportService = reportService;
        _notificationService = notificationService;
        _stationSettings = stationSettings;
        _fastEntrySearch = fastEntrySearch;
        _debtExport = debtExport;
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
    public ObservableCollection<AutocompleteSuggestionItem> CustomerSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> VehicleSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> CargoSuggestions { get; } = [];

    partial void OnCustomerKeywordChanged(string? value) =>
        _ = RefreshSuggestionsAsync(CustomerSuggestions, value, t => _fastEntrySearch.SearchCustomersAsync(t));

    partial void OnCargoTypeKeywordChanged(string? value) =>
        _ = RefreshSuggestionsAsync(CargoSuggestions, value, t => _fastEntrySearch.SearchCargoTypesAsync(t));

    partial void OnLicensePlateKeywordChanged(string? value) =>
        _ = RefreshSuggestionsAsync(VehicleSuggestions, value, t => _fastEntrySearch.SearchVehiclesAsync(t, CustomerKeyword));

    private static async Task RefreshSuggestionsAsync(
        ObservableCollection<AutocompleteSuggestionItem> target,
        string? value,
        Func<string, Task<IReadOnlyList<AutocompleteSuggestionItem>>> search)
    {
        try
        {
            await Task.Delay(200);
            var items = await search(value ?? string.Empty);
            target.Clear();
            foreach (var item in items.Where(i => !i.IsNewEntryOption))
                target.Add(item);
        }
        catch (Exception ex) when (ex is TaskCanceledException or InvalidOperationException)
        {
        }
    }

    /// <summary>Double-click on an empty filter box: list every catalog entry to pick from.</summary>
    public async Task ShowHistoryAsync(AutocompleteField field, AutoCompleteTextBox box)
    {
        try
        {
            var (items, target) = field switch
            {
                AutocompleteField.Customer => (await _fastEntrySearch.GetCustomerHistoryAsync(), CustomerSuggestions),
                AutocompleteField.Vehicle => (await _fastEntrySearch.GetVehicleHistoryAsync(), VehicleSuggestions),
                _ => (await _fastEntrySearch.GetCargoTypeHistoryAsync(), CargoSuggestions)
            };

            target.Clear();
            foreach (var item in items)
                target.Add(item);

            if (items.Count > 0)
                box.OpenHistoryDropDown();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    /// <summary>Re-runs the filter on screen (even if LỌC wasn't pressed after the last edit) for printing or preview.</summary>
    private async Task<(ReportFilter Filter, ReportResult Result, StationSettingsDto Station)?> PrepareReportAsync(string title)
    {
        var filter = BuildFilter();
        var result = await _reportService.QueryAsync(filter);
        ApplyResult(result);

        if (result.Rows.Count == 0)
        {
            MessageBox.Show(
                "Không có phiếu nào theo bộ lọc hiện tại.",
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return null;
        }

        var station = await _stationSettings.GetStationAsync();
        return (filter, result, station);
    }

    private static ReportPrintModel BuildPrintModel(ReportFilter filter, ReportResult result, StationSettingsDto station) =>
        new()
        {
            Station = station,
            Period = ReportPrintHelper.DescribePeriod(filter),
            Criteria = ReportPrintHelper.DescribeCriteria(filter),
            Rows = result.Rows,
            Summary = result.Summary,
            DistinctVehicles = ReportPrintHelper.CountDistinctVehicles(result.Rows)
        };

    [RelayCommand]
    private async Task PreviewReportAsync()
    {
        if (IsBusy)
            return;

        var wantsPrint = false;
        IsBusy = true;
        try
        {
            var prepared = await PrepareReportAsync("Xem báo cáo");
            if (prepared is null)
                return;

            var (filter, result, station) = prepared.Value;

            // Previewed as designed (landscape), one image per page; printing rebuilds it for the chosen sheet.
            var document = ReportPrintDocumentBuilder.Build(BuildPrintModel(filter, result, station), 1122.5, 793.7);
            var pages = ReportPrintDocumentBuilder.RenderPages(document, 1.5);

            var window = new ReportPreviewWindow(pages, result.Summary.TicketCount)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            wantsPrint = window.ShowDialog() == true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi xem báo cáo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }

        if (wantsPrint)
            await PrintReportAsync();
    }

    [RelayCommand]
    private async Task PrintReportAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var prepared = await PrepareReportAsync("In báo cáo");
            if (prepared is null)
                return;

            var (filter, result, station) = prepared.Value;

            var dialog = new PrintDialog();
            try
            {
                dialog.PrintTicket.PageOrientation = PageOrientation.Portrait;
            }
            catch (Exception)
            {
                // No usable default printer ticket — the dialog still lets the user pick one.
            }

            if (dialog.ShowDialog() != true)
                return;

            var model = BuildPrintModel(filter, result, station);

            // The report is designed landscape. On a portrait sheet (the printer's normal feed, e.g. Brother)
            // the same page is turned 90 degrees; on a landscape sheet it prints as designed.
            var sheetWidth = dialog.PrintableAreaWidth;
            var sheetHeight = dialog.PrintableAreaHeight;
            var portraitSheet = sheetHeight > sheetWidth;
            var document = ReportPrintDocumentBuilder.Build(
                model,
                portraitSheet ? sheetHeight : sheetWidth,
                portraitSheet ? sheetWidth : sheetHeight,
                rotateToPortrait: portraitSheet);
            dialog.PrintDocument(document.DocumentPaginator, "Báo cáo phiếu cân");
            StatusMessage = $"Đã gửi lệnh in báo cáo ({result.Summary.TicketCount} phiếu).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi in báo cáo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

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

    /// <summary>Exports every completed ticket in the date range (all customers) for the debt app to import.</summary>
    [RelayCommand]
    private async Task ExportDebtFileAsync()
    {
        if (IsBusy)
            return;

        var from = FromDate ?? new DateTime(2000, 1, 1);
        var to = ToDate ?? DateTime.Today;
        var station = await _stationSettings.GetStationAsync();
        if (string.IsNullOrWhiteSpace(station.DebtStationCode))
        {
            MessageBox.Show(
                "Chưa đặt mã điểm cân.\n\nVào Cài đặt → Cấu hình trạm cân, mục “Điểm cân”, nhập mã (ví dụ DAKHA) và tên điểm, rồi lưu.",
                "Xuất file công nợ",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Xuất file công nợ của điểm cân {station.DebtStationName ?? station.DebtStationCode}\n" +
            $"Từ ngày {from:dd/MM/yyyy} đến ngày {to:dd/MM/yyyy}, tất cả khách hàng.\n\n" +
            "Tiếp tục?",
            "Xuất file công nợ",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK)
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "File công nợ (*.json)|*.json",
            FileName = $"CanXe-{station.DebtStationCode}-{DateTime.Now:yyyyMMdd-HHmm}.json",
            DefaultExt = ".json"
        };
        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        try
        {
            var result = await _debtExport.ExportAsync(from, to, dialog.FileName);
            if (!result.Success)
            {
                MessageBox.Show(
                    result.ErrorMessage ?? "Không thể xuất file công nợ.",
                    "Lỗi xuất file công nợ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            await _notificationService.ShowExportSuccessToastAsync(result.FilePath);
            StatusMessage = $"Đã xuất file công nợ: {result.TicketCount} phiếu" +
                            (result.DeletedCount > 0 ? $" (+{result.DeletedCount} phiếu đã xóa)" : "") + ".";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi xuất file công nợ", MessageBoxButton.OK, MessageBoxImage.Warning);
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
