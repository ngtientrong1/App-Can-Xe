using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

public sealed class Phase4Rc20DirectPrintTests
{
    [Fact]
    public async Task ExecuteMainPrintAsync_SetsPreparingStatus_ThenResetsIsPrinting()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var workflow = new RecordingPrintWorkflow();
        var notification = new RecordingPrintNotificationService();
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync(workflow, notification);

        vm.FormMode = TicketFormMode.Viewing;
        vm.ActiveTicketId = 5;
        await vm.ExecuteMainPrintAsync();

        Assert.Equal("ĐÃ GỬI ĐẾN MÁY IN", vm.StatusMessage);
        Assert.False(vm.IsPrinting);
        Assert.Equal(1, workflow.PrintInvocationCount);
    }

    [Fact]
    public async Task ExecuteMainPrintAsync_ResolvesEditingTicket()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var workflow = new RecordingPrintWorkflow();
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync(workflow, new RecordingPrintNotificationService());
        vm.FormMode = TicketFormMode.Editing;
        vm.EditingTicketId = 9;
        await vm.ExecuteMainPrintAsync();
        Assert.Equal(1, workflow.PrintInvocationCount);
        Assert.Equal(9, workflow.LastTicketId);
    }

    [Fact]
    public async Task BuildPrintModelAsync_StillLoadsFromDatabase()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        await DependencyInjection.InitializeDatabaseAsync(host.Provider, host.AppPaths.DatabasePath);
        var service = host.Provider.GetRequiredService<WeighTicketService>();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(18500m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);

        var model = await host.Provider.GetRequiredService<IWeighTicketPrintWorkflow>()
            .BuildPrintModelAsync(save.SavedTicket!.Id);
        Assert.NotNull(model);
    }
}
