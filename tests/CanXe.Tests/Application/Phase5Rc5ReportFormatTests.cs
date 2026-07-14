using CanXe.Application.Services;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Tests.Support;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class Phase5Rc5ReportFormatTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Theory]
    [InlineData(9420, "9.420")]
    [InlineData(7250, "7.250")]
    [InlineData(37550, "37.550")]
    [InlineData(80560000, "80.560.000")]
    public void VietnameseNumberFormatter_UsesDotThousandsSeparator(decimal value, string expected)
    {
        Assert.Equal(expected, VietnameseNumberFormatter.FormatWeight(value));
        Assert.Equal(expected, VietnameseNumberFormatter.FormatMoney(value));
        Assert.Equal(expected, VietnameseNumberFormatter.FormatInteger(value));
    }

    [Fact]
    public void VietnameseNumberFormatter_NullOrMissing_DisplaysDash()
    {
        Assert.Equal("—", VietnameseNumberFormatter.FormatWeight(null));
        Assert.Equal("—", VietnameseNumberFormatter.FormatMoney(null));
        Assert.Equal("—", VietnameseNumberFormatter.FormatInteger((decimal?)null));
    }

    [Fact]
    public async Task ExportExcelAsync_DoesNotIncludeStatusColumn()
    {
        await SeedTicketAsync("Khách Excel", "51E-77777", "Gạo", 18000m, 17000m, unitPrice: 1500m);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var report = scope.ServiceProvider.GetRequiredService<IReportService>();
        var path = Path.Combine(Path.GetTempPath(), $"canxe-rc5-{Guid.NewGuid():N}.xlsx");

        try
        {
            var export = await report.ExportExcelAsync(
                new ReportFilter
                {
                    FromDate = DateTimeOffset.Now.AddDays(-30),
                    ToDate = DateTimeOffset.Now.AddDays(1)
                },
                path,
                "Trạm thử");

            Assert.True(export.Success);

            using var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheet("BaoCaoPhieuCan");
            var headerRow = FindHeaderRow(sheet);
            var headers = Enumerable.Range(1, 13)
                .Select(col => sheet.Cell(headerRow, col).GetString())
                .ToArray();

            Assert.DoesNotContain("Trạng thái", headers);
            Assert.Equal("Khách hàng", headers[3]);
            Assert.Equal("Thành tiền", headers[11]);
            Assert.Equal("Ghi chú", headers[12]);

            var totalRow = sheet.LastRowUsed()!.RowNumber();
            Assert.Equal("Tổng cộng", sheet.Cell(totalRow, 1).GetString());
            Assert.Equal(1, (int)sheet.Cell(totalRow, 3).GetDouble());
            Assert.Equal("#,##0", sheet.Cell(totalRow, 12).Style.NumberFormat.Format);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportExcelAsync_NumericColumns_UseThousandsFormat()
    {
        await SeedTicketAsync("Khách số", "51N-1", "Gạo", 9420m, 7250m, unitPrice: 37550m);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var report = scope.ServiceProvider.GetRequiredService<IReportService>();
        var path = Path.Combine(Path.GetTempPath(), $"canxe-rc5-fmt-{Guid.NewGuid():N}.xlsx");

        try
        {
            var export = await report.ExportExcelAsync(
                new ReportFilter
                {
                    FromDate = DateTimeOffset.Now.AddDays(-30),
                    ToDate = DateTimeOffset.Now.AddDays(1)
                },
                path,
                "Trạm thử");

            Assert.True(export.Success);

            using var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheet("BaoCaoPhieuCan");
            var dataRow = FindHeaderRow(sheet) + 1;

            Assert.Equal("#,##0", sheet.Cell(dataRow, 7).Style.NumberFormat.Format);
            Assert.Equal(9420d, sheet.Cell(dataRow, 7).GetDouble(), 0);
            Assert.Equal("#,##0", sheet.Cell(dataRow, 11).Style.NumberFormat.Format);
            Assert.Equal(37550d, sheet.Cell(dataRow, 11).GetDouble(), 0);
            Assert.Equal("#,##0", sheet.Cell(dataRow, 12).Style.NumberFormat.Format);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task QueryAsync_WorkflowFilter_StillWorksAfterStatusColumnRemoval()
    {
        await SeedTicketAsync("Khách chờ", "51X-00001", "Gạo", 18000m, null, saveSecondWeigh: false);
        await SeedTicketAsync("Khách hoàn tất", "51Y-00002", "Gạo", 18000m, 17000m);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var report = scope.ServiceProvider.GetRequiredService<IReportService>();
        var awaiting = await report.QueryAsync(new ReportFilter
        {
            State = ReportWorkflowFilter.AwaitingSecondWeigh,
            FromDate = DateTimeOffset.Now.AddDays(-30),
            ToDate = DateTimeOffset.Now.AddDays(1)
        });

        Assert.Single(awaiting.Rows);
        Assert.Equal("51X-00001", awaiting.Rows[0].LicensePlate);
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
        decimal? unitPrice = null,
        bool saveSecondWeigh = true)
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
        else if (!saveSecondWeigh)
        {
            var partial = await service.SaveAsync(draft);
            Assert.True(partial.Success);
            return;
        }

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);
    }
}
