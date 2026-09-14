using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Infrastructure.Services;
using CanXe.Tests.Support;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class Phase5ReportExcelTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task QueryAsync_FiltersByCustomerKeyword()
    {
        await SeedTicketAsync("Công ty Alpha", "51A-11111", "Gạo", 18000m, 17000m);
        await SeedTicketAsync("Công ty Beta", "51B-22222", "Ngô", 19000m, 18000m);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var report = scope.ServiceProvider.GetRequiredService<IReportService>();
        var result = await report.QueryAsync(new ReportFilter
        {
            CustomerKeyword = "Alpha",
            FromDate = DateTimeOffset.Now.AddDays(-30),
            ToDate = DateTimeOffset.Now.AddDays(1)
        });

        Assert.Single(result.Rows);
        Assert.Contains("Alpha", result.Rows[0].CustomerName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_FiltersByLicensePlateAndCargoType()
    {
        await SeedTicketAsync("Khách 1", "51C-99999", "Đường", 18000m, 17000m);
        await SeedTicketAsync("Khách 2", "51D-88888", "Muối", 18000m, 17000m);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var report = scope.ServiceProvider.GetRequiredService<IReportService>();

        var byPlate = await report.QueryAsync(new ReportFilter
        {
            LicensePlateKeyword = "99999",
            FromDate = DateTimeOffset.Now.AddDays(-30),
            ToDate = DateTimeOffset.Now.AddDays(1)
        });
        Assert.Single(byPlate.Rows);
        Assert.Equal("51C-999.99", byPlate.Rows[0].LicensePlate);

        var byCargo = await report.QueryAsync(new ReportFilter
        {
            CargoTypeKeyword = "Muối",
            FromDate = DateTimeOffset.Now.AddDays(-30),
            ToDate = DateTimeOffset.Now.AddDays(1)
        });
        Assert.Single(byCargo.Rows);
        Assert.Equal("Muối", byCargo.Rows[0].CargoTypeName);
    }

    [Fact]
    public async Task QueryAsync_FiltersAwaitingSecondWeigh_AndExcludesSoftDeleted()
    {
        await SeedTicketAsync("Khách chờ", "51X-00001", "Gạo", 18000m, null, saveSecondWeigh: false);
        var completedId = await SeedTicketAsync("Khách hoàn tất", "51Y-00002", "Gạo", 18000m, 17000m);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var deleteService = new TicketDeleteService(
            scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>(),
            new DeveloperAuthorizationService(new AppSettings
            {
                DeveloperMode = true,
                DeveloperTicketEditEnabled = true
            }));
        await deleteService.SoftDeleteAsync(completedId);

        var report = scope.ServiceProvider.GetRequiredService<IReportService>();
        var awaiting = await report.QueryAsync(new ReportFilter
        {
            State = ReportWorkflowFilter.AwaitingSecondWeigh,
            FromDate = DateTimeOffset.Now.AddDays(-30),
            ToDate = DateTimeOffset.Now.AddDays(1)
        });
        Assert.Single(awaiting.Rows);
        Assert.Equal("51X-000.01", awaiting.Rows[0].LicensePlate);

        var all = await report.QueryAsync(new ReportFilter
        {
            FromDate = DateTimeOffset.Now.AddDays(-30),
            ToDate = DateTimeOffset.Now.AddDays(1)
        });
        Assert.DoesNotContain(all.Rows, r => r.LicensePlate == "51Y-000.02");
        Assert.Contains(all.Rows, r => r.LicensePlate == "51X-000.01");
    }

    [Fact]
    public async Task QueryAsync_SummaryTotals_AreCorrect()
    {
        await SeedTicketAsync("Khách A", "51A-1", "Gạo", 20000m, 15000m, unitPrice: 1000m);
        await SeedTicketAsync("Khách B", "51A-2", "Gạo", 18000m, 16000m, unitPrice: 2000m);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var report = scope.ServiceProvider.GetRequiredService<IReportService>();
        var result = await report.QueryAsync(new ReportFilter
        {
            FromDate = DateTimeOffset.Now.AddDays(-30),
            ToDate = DateTimeOffset.Now.AddDays(1)
        });

        Assert.Equal(2, result.Summary.TicketCount);
        Assert.Equal(7000m, result.Summary.TotalNetWeightKg);
        Assert.True(result.Summary.TotalAmountVnd > 0);
    }

    [Fact]
    public async Task ExportExcelAsync_WritesFormattedWorkbook()
    {
        await SeedTicketAsync("Khách Excel", "51E-77777", "Gạo", 18000m, 17000m, unitPrice: 1500m);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var report = scope.ServiceProvider.GetRequiredService<IReportService>();
        var path = Path.Combine(Path.GetTempPath(), $"canxe-report-{Guid.NewGuid():N}.xlsx");

        try
        {
            var export = await report.ExportExcelAsync(
                new ReportFilter
                {
                    FromDate = DateTimeOffset.Now.AddDays(-30),
                    ToDate = DateTimeOffset.Now.AddDays(1)
                },
                path,
                "Trạm thử nghiệm");

            Assert.True(export.Success);
            Assert.True(File.Exists(path));

            using var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheet("BaoCaoPhieuCan");
            Assert.Equal("Trạm thử nghiệm", sheet.Cell(1, 1).GetString());
            Assert.Equal("STT", sheet.Cell(5, 1).GetString());
            Assert.Equal("Tổng cộng", sheet.Cell(sheet.LastRowUsed()!.RowNumber(), 1).GetString());
            Assert.True(sheet.Column(2).Width > 0);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task QueryAsync_UsesAsyncDatabaseQuery()
    {
        await SeedTicketAsync("Khách async", "51Z-1", "Gạo", 18000m, 17000m);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var report = scope.ServiceProvider.GetRequiredService<IReportService>();
        var result = await report.QueryAsync(new ReportFilter
        {
            FromDate = DateTimeOffset.Now.AddDays(-30),
            ToDate = DateTimeOffset.Now.AddDays(1)
        });

        Assert.NotEmpty(result.Rows);
    }

    private async Task<int> SeedTicketAsync(
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
            return partial.SavedTicket!.Id;
        }

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);
        return save.SavedTicket!.Id;
    }
}
