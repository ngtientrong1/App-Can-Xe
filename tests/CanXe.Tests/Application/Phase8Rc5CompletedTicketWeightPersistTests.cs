using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using CanXe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public sealed class Phase8Rc5CompletedTicketWeightPersistTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private WeighTicketService CreateService() =>
        _factory.Provider.CreateScope().ServiceProvider.GetRequiredService<WeighTicketService>();

    private IScaleService CreateScale() =>
        _factory.Provider.GetRequiredService<IScaleService>();

    private async Task<int> SaveCompletedTicketAsync(
        string customer = "Khách rc5",
        string plate = "51RC5-01",
        string cargo = "Hàng rc5",
        decimal unitPrice = 500m,
        decimal w1 = 8000m,
        decimal w2 = 12000m)
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft
        {
            DraftCustomer = customer,
            DraftVehicle = plate,
            DraftCargoType = cargo,
            DraftUnitPrice = unitPrice,
            DraftNotes = "Ghi chú rc5"
        };
        scale.SetManualWeightKg(w1);
        Assert.True((await service.CaptureWeightAsync(draft, 1)).Success);
        scale.SetManualWeightKg(w2);
        Assert.True((await service.CaptureWeightAsync(draft, 2)).Success);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        return save.SavedTicket!.Id;
    }

    [Fact]
    public async Task CompletedTicket_AdminEditWeight1_PersistsToDatabase()
    {
        var ticketId = await SaveCompletedTicketAsync();
        var service = CreateService();
        var edit = await service.LoadTicketForEditAsync(ticketId);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 1, 9000m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;

        var update = await service.UpdateTicketAsync(edit, "admin");
        Assert.True(update.Success, update.ErrorMessage);

        var detail = await service.GetTicketDetailAsync(ticketId);
        Assert.Equal(9000m, detail.Weight1Kg);
        Assert.Equal(12000m, detail.Weight2Kg);
        Assert.Equal(3000m, detail.NetWeightKg);
    }

    [Fact]
    public async Task CompletedTicket_AdminEditWeight2_PersistsToDatabase()
    {
        var ticketId = await SaveCompletedTicketAsync();
        var service = CreateService();
        var edit = await service.LoadTicketForEditAsync(ticketId);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 2, 13000m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;

        Assert.True((await service.UpdateTicketAsync(edit, "admin")).Success);

        var detail = await service.GetTicketDetailAsync(ticketId);
        Assert.Equal(8000m, detail.Weight1Kg);
        Assert.Equal(13000m, detail.Weight2Kg);
        Assert.Equal(5000m, detail.NetWeightKg);
    }

    [Fact]
    public async Task CompletedTicket_AdminEditBothWeights_RecalculatesNet()
    {
        var ticketId = await SaveCompletedTicketAsync();
        var service = CreateService();
        var edit = await service.LoadTicketForEditAsync(ticketId);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 1, 9000m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 2, 12000m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;

        var update = await service.UpdateTicketAsync(edit, "admin");
        Assert.True(update.Success, update.ErrorMessage);
        Assert.Equal(12000m, update.UpdatedTicket!.GrossWeightKg);
        Assert.Equal(9000m, update.UpdatedTicket.TareWeightKg);
        Assert.True(update.UpdatedTicket.NetWeightKg is >= 2900m and <= 3000m);

        var detail = await service.GetTicketDetailAsync(ticketId);
        Assert.Equal(9000m, detail.Weight1Kg);
        Assert.Equal(12000m, detail.Weight2Kg);
        Assert.True(detail.NetWeightKg is >= 2900m and <= 3000m);
        Assert.NotNull(detail.BillableWeightKg);
    }

    [Fact]
    public async Task CompletedTicket_AdminEditWeight_PreservesMetadata()
    {
        var ticketId = await SaveCompletedTicketAsync(
            customer: "Khách giữ meta",
            plate: "51RC5-META",
            cargo: "Cát rc5");

        var service = CreateService();
        var edit = await service.LoadTicketForEditAsync(ticketId);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 1, 9500m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;
        Assert.True((await service.UpdateTicketAsync(edit, "admin")).Success);

        var detail = await service.GetTicketDetailAsync(ticketId);
        Assert.Equal("Khách giữ meta", detail.CustomerName);
        Assert.Equal("51RC5-META", detail.LicensePlate);
        Assert.Equal("Cát rc5", detail.CargoTypeName);
    }

    [Fact]
    public async Task CompletedTicket_AdminEditWeight_PreservesPriceAndNote()
    {
        var ticketId = await SaveCompletedTicketAsync(unitPrice: 750m);
        var service = CreateService();
        var edit = await service.LoadTicketForEditAsync(ticketId);
        Assert.Equal(750m, edit.DraftUnitPrice);
        Assert.Equal("Ghi chú rc5", edit.DraftNotes);

        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 1, 8500m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;
        Assert.True((await service.UpdateTicketAsync(edit, "admin")).Success);

        var detail = await service.GetTicketDetailAsync(ticketId);
        Assert.Equal(750m, detail.UnitPriceVndPerKg);
        Assert.Equal("Ghi chú rc5", detail.Notes);
        Assert.NotNull(detail.TotalAmountVnd);
        Assert.True(detail.TotalAmountVnd > 2_000_000m);
    }

    [Fact]
    public async Task CompletedTicket_AdminEditWeight_UpdatesListRow()
    {
        var ticketId = await SaveCompletedTicketAsync();
        var service = CreateService();
        var edit = await service.LoadTicketForEditAsync(ticketId);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 1, 9000m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;
        var update = await service.UpdateTicketAsync(edit, "admin");
        Assert.True(update.Success, update.ErrorMessage);
        Assert.Equal(3000m, update.UpdatedTicket!.NetWeightKg);

        var list = await service.GetFilteredAsync(new WeighTicketFilter());
        var row = Assert.Single(list, t => t.Id == ticketId);
        Assert.Equal(3000m, row.NetWeightKg);
    }

    [Fact]
    public async Task CompletedTicket_AdminEditWeight_DoesNotMoveRowToTop()
    {
        var olderId = await SaveCompletedTicketAsync(plate: "51RC5-OLD", w1: 8000m, w2: 12000m);
        await Task.Delay(20);
        var newerId = await SaveCompletedTicketAsync(plate: "51RC5-NEW", customer: "Khách mới", w1: 7000m, w2: 11000m);

        var service = CreateService();
        var edit = await service.LoadTicketForEditAsync(olderId);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 1, 9000m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;
        Assert.True((await service.UpdateTicketAsync(edit, "admin")).Success);

        var list = await service.GetFilteredAsync(new WeighTicketFilter());
        Assert.Equal(newerId, list[0].Id);
        Assert.Equal(olderId, list[1].Id);
        Assert.False(TicketListCollectionPolicy.TreatSavedTicketAsNewListEntry(wasContinuationSave: true));
    }

    [Fact]
    public async Task CompletedTicket_AdminEditWeight_DoesNotCreateDuplicateEvents()
    {
        var ticketId = await SaveCompletedTicketAsync();
        var service = CreateService();
        var edit = await service.LoadTicketForEditAsync(ticketId);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 1, 9000m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;
        Assert.True((await service.UpdateTicketAsync(edit, "admin")).Success);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CanXeDbContext>();
        var events = await db.WeighEvents.Where(e => e.WeighTicketId == ticketId).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.Equal(1, events.Count(e => e.Sequence == 1));
        Assert.Equal(1, events.Count(e => e.Sequence == 2));
    }

    [Fact]
    public async Task CompletedTicket_AdminEditWeight_WritesAudit()
    {
        var ticketId = await SaveCompletedTicketAsync();
        var service = CreateService();
        var edit = await service.LoadTicketForEditAsync(ticketId);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 1, 9000m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;
        Assert.True((await service.UpdateTicketAsync(edit, "admin")).Success);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CanXeDbContext>();
        var audits = await db.AuditLogs.Where(a => a.TicketId == ticketId).ToListAsync();
        Assert.Contains(audits, a => a.FieldName == "Weight1");
    }

    [Fact]
    public async Task CompletedTicket_AdminEditWeight_ReportUsesUpdatedWeights()
    {
        var ticketId = await SaveCompletedTicketAsync();
        var service = CreateService();
        var edit = await service.LoadTicketForEditAsync(ticketId);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 1, 9000m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;
        Assert.True((await service.UpdateTicketAsync(edit, "admin")).Success);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportService>();
        var result = await reports.QueryAsync(new ReportFilter());
        var row = Assert.Single(result.Rows, r => r.TicketId == ticketId);
        Assert.Equal(3000m, row.NetWeightKg);
    }

    [Fact]
    public async Task CompletedTicket_AdminEditWeight_PrintTargetUsesUpdatedTicket()
    {
        var ticketId = await SaveCompletedTicketAsync();
        var service = CreateService();
        var edit = await service.LoadTicketForEditAsync(ticketId);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 1, 9000m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;
        Assert.True((await service.UpdateTicketAsync(edit, "admin")).Success);

        var detail = await service.GetTicketDetailAsync(ticketId);
        Assert.Equal(9000m, detail.Weight1Kg);
        Assert.Equal(12000m, detail.Weight2Kg);
        Assert.Equal(3000m, detail.NetWeightKg);

        var printModel = WeighTicketPrintModelMapper.FromDetail(
            detail,
            new StationSettingsDto { StationName = "Test" },
            new PrintSettingsDto { ShowPrice = true },
            isReprint: false);
        Assert.Equal(9000m, printModel.Weight1Kg);
        Assert.Equal(12000m, printModel.Weight2Kg);
        Assert.Equal(3000m, printModel.NetWeightKg);
    }

    [Fact]
    public async Task CompletedTicket_AdminEditWeight_ExcelUsesUpdatedWeights()
    {
        var ticketId = await SaveCompletedTicketAsync();
        var service = CreateService();
        var edit = await service.LoadTicketForEditAsync(ticketId);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 1, 9000m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;
        Assert.True((await service.UpdateTicketAsync(edit, "admin")).Success);

        await using var scope = _factory.Provider.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportService>();
        var result = await reports.QueryAsync(new ReportFilter());
        var row = Assert.Single(result.Rows, r => r.TicketId == ticketId);
        Assert.Equal(12000m, row.GrossWeightKg);
        Assert.Equal(9000m, row.TareWeightKg);
        Assert.Equal(3000m, row.NetWeightKg);
    }
}
