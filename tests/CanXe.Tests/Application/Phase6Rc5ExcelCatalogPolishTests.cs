using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Tests.Support;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class Phase6Rc5ExcelCatalogPolishTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task ExcelExport_SortsOldestToNewest()
    {
        await SeedTicketAsync("Khách cũ", "51OLD-001", "Gạo", 18000m, 17000m);
        await Task.Delay(40);
        await SeedTicketAsync("Khách mới", "51NEW-002", "Ngô", 19000m, 18000m);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var report = scope.ServiceProvider.GetRequiredService<IReportService>();
        var query = await report.QueryAsync(new ReportFilter
        {
            FromDate = DateTimeOffset.Now.AddDays(-30),
            ToDate = DateTimeOffset.Now.AddDays(1)
        });
        Assert.True(query.Rows.Count >= 2);

        // UI/query may still be newest-first; Excel must be oldest-first.
        Assert.Equal("51NEW-002", query.Rows[0].LicensePlate);

        var path = Path.Combine(Path.GetTempPath(), $"canxe-p6rc5-sort-{Guid.NewGuid():N}.xlsx");
        try
        {
            var export = await report.ExportExcelAsync(
                new ReportFilter
                {
                    FromDate = DateTimeOffset.Now.AddDays(-30),
                    ToDate = DateTimeOffset.Now.AddDays(1)
                },
                path,
                "Trạm rc5");
            Assert.True(export.Success);

            using var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheet("BaoCaoPhieuCan");
            var headerRow = FindHeaderRow(sheet);
            Assert.Equal("51OLD-001", sheet.Cell(headerRow + 1, 5).GetString());
            Assert.Equal("51NEW-002", sheet.Cell(headerRow + 2, 5).GetString());
            Assert.Equal(1, (int)sheet.Cell(headerRow + 1, 1).GetDouble());
            Assert.Equal(2, (int)sheet.Cell(headerRow + 2, 1).GetDouble());
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ExcelExport_HasHeaderStyle()
    {
        await SeedTicketAsync("Khách style", "51ST-1", "Gạo", 18000m, 17000m);
        var path = await ExportTempAsync("Trạm header style");

        try
        {
            using var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheet("BaoCaoPhieuCan");
            Assert.True(sheet.Cell(1, 1).Style.Font.Bold);
            Assert.True(sheet.Cell(1, 1).Style.Font.FontSize >= 14);

            var headerRow = FindHeaderRow(sheet);
            Assert.Equal("STT", sheet.Cell(headerRow, 1).GetString());
            Assert.True(sheet.Cell(headerRow, 1).Style.Font.Bold);
            Assert.False(sheet.Cell(headerRow, 1).Style.Fill.BackgroundColor.Equals(XLColor.NoColor));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ExcelExport_HasAutoFilterAndFreezePane()
    {
        await SeedTicketAsync("Khách filter", "51AF-1", "Gạo", 18000m, 17000m);
        var path = await ExportTempAsync("Trạm filter");

        try
        {
            using var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheet("BaoCaoPhieuCan");
            var headerRow = FindHeaderRow(sheet);
            Assert.Equal(headerRow, sheet.SheetView.SplitRow);
            Assert.True(sheet.AutoFilter.IsEnabled);
            Assert.NotNull(sheet.AutoFilter.Range);
            Assert.Equal(headerRow, sheet.AutoFilter.Range!.FirstRow().RowNumber());
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ExcelExport_HasTotalsRow()
    {
        await SeedTicketAsync("Khách tổng A", "51T-1", "Gạo", 20000m, 15000m, unitPrice: 1000m);
        await SeedTicketAsync("Khách tổng B", "51T-2", "Gạo", 18000m, 16000m, unitPrice: 2000m);
        var path = await ExportTempAsync("Trạm tổng");

        try
        {
            using var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheet("BaoCaoPhieuCan");
            var totalRow = sheet.LastRowUsed()!.RowNumber();
            Assert.Equal("Tổng cộng", sheet.Cell(totalRow, 1).GetString());
            Assert.True(sheet.Cell(totalRow, 1).Style.Font.Bold);
            Assert.Equal(XLBorderStyleValues.Thin, sheet.Cell(totalRow, 9).Style.Border.TopBorder);
            Assert.True(sheet.Cell(totalRow, 9).GetDouble() > 0); // Hàng
            Assert.True(sheet.Cell(totalRow, 10).GetDouble() > 0); // KL tính tiền
            Assert.True(sheet.Cell(totalRow, 12).GetDouble() > 0); // Thành tiền
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task DeletedCustomer_NotShownInSuggestions()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var search = scope.ServiceProvider.GetRequiredService<FastEntrySearchService>();

        await customers.UpsertAsync("Khách p6rc5 gợi ý");
        var row = (await catalog.ListAsync(CatalogTab.Customer, "Khách p6rc5 gợi ý")).Single();
        await catalog.HideCustomerAsync(row.Id);

        var suggestions = await search.SearchCustomersAsync("p6rc5 gợi");
        Assert.DoesNotContain(suggestions, i => i.PrimaryText == "Khách p6rc5 gợi ý");
    }

    [Fact]
    public async Task DeleteCustomer_DoesNotAffectHistoricalTickets()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách p6rc5 lịch sử" };
        scale.SetManualWeightKg(10000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(4000m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);

        var customer = await scope.ServiceProvider.GetRequiredService<ICustomerRepository>()
            .FindActiveByNormalizedNameAsync(CanXe.Domain.Services.TextNormalizer.Normalize("Khách p6rc5 lịch sử"));
        Assert.NotNull(customer);
        await catalog.HideCustomerAsync(customer!.Id);

        var detail = await service.GetTicketDetailAsync(save.SavedTicket!.Id);
        Assert.Equal("Khách p6rc5 lịch sử", detail.CustomerName);
    }

    private async Task<string> ExportTempAsync(string station)
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var report = scope.ServiceProvider.GetRequiredService<IReportService>();
        var path = Path.Combine(Path.GetTempPath(), $"canxe-p6rc5-{Guid.NewGuid():N}.xlsx");
        var export = await report.ExportExcelAsync(
            new ReportFilter
            {
                FromDate = DateTimeOffset.Now.AddDays(-30),
                ToDate = DateTimeOffset.Now.AddDays(1)
            },
            path,
            station);
        Assert.True(export.Success);
        return path;
    }

    private static int FindHeaderRow(IXLWorksheet sheet)
    {
        for (var row = 1; row <= sheet.LastRowUsed()!.RowNumber(); row++)
        {
            if (sheet.Cell(row, 1).GetString() == "STT")
                return row;
        }

        throw new InvalidOperationException("Header row not found.");
    }

    private async Task SeedTicketAsync(
        string customer,
        string plate,
        string cargo,
        decimal weight1Kg,
        decimal? weight2Kg,
        decimal? unitPrice = null)
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft
        {
            DraftCustomer = customer,
            DraftVehicle = plate,
            DraftCargoType = cargo,
            DraftUnitPrice = unitPrice
        };

        scale.SetManualWeightKg(weight1Kg);
        await service.CaptureWeightAsync(draft, 1);
        if (weight2Kg is decimal w2)
        {
            scale.SetManualWeightKg(w2);
            await service.CaptureWeightAsync(draft, 2);
        }

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
    }
}
