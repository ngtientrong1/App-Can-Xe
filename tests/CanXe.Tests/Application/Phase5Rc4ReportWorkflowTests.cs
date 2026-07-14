using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Infrastructure.Services;
using CanXe.Tests.Support;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class Phase5Rc4ReportWorkflowTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public void ReportDateRange_EndExclusive_IncludesFullLastDay()
    {
        var filter = new ReportFilter
        {
            FromDate = new DateTimeOffset(2026, 6, 27, 0, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 6, 27))),
            ToDate = new DateTimeOffset(2026, 7, 4, 0, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 7, 4)))
        };

        var (from, toExclusive) = ReportDateRange.Normalize(filter);
        var endOfJuly4 = new DateTimeOffset(2026, 7, 4, 23, 59, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 7, 4, 23, 59, 0)));

        Assert.True(ReportDateRange.MatchesTicketDate(endOfJuly4, from, toExclusive));
        Assert.False(ReportDateRange.MatchesTicketDate(toExclusive!.Value, from, toExclusive));
    }

    [Fact]
    public async Task QueryAsync_ReturnsTicketsInLocalDateRange()
    {
        await SeedTicketAsync("Khách A", "51A-1", "Gạo", 18000m, 17000m);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var report = scope.ServiceProvider.GetRequiredService<IReportService>();
        var result = await report.QueryAsync(new ReportFilter
        {
            FromDate = new DateTimeOffset(DateTime.Today.AddDays(-30)),
            ToDate = new DateTimeOffset(DateTime.Today)
        });

        Assert.NotEmpty(result.Rows);
        Assert.True(result.Summary.TicketCount > 0);
    }

    [Fact]
    public async Task ExportExcelAsync_DoesNotThrowUtcOffsetError()
    {
        await SeedTicketAsync("Khách Excel", "51E-1", "Gạo", 18000m, 17000m, unitPrice: 1500m);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var report = scope.ServiceProvider.GetRequiredService<IReportService>();
        var path = Path.Combine(Path.GetTempPath(), $"canxe-rc4-{Guid.NewGuid():N}.xlsx");

        try
        {
            var export = await report.ExportExcelAsync(
                new ReportFilter
                {
                    FromDate = new DateTimeOffset(DateTime.Today.AddDays(-30)),
                    ToDate = new DateTimeOffset(DateTime.Today)
                },
                path,
                "Trạm thử");

            Assert.True(export.Success);
            using var workbook = new XLWorkbook(path);
            Assert.NotNull(workbook.Worksheet("BaoCaoPhieuCan"));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_WithoutWeight1_FailsWithGuidanceMessage()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var result = await service.SaveAsync(new WeighTicketDraft
        {
            DraftCustomer = "Khách",
            DraftVehicle = "51A-1"
        });

        Assert.False(result.Success);
        Assert.Contains("cân lần 1", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
        Assert.True(save.Success);
    }
}
