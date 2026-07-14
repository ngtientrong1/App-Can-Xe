using System.IO;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase5Rc8CustomerOnlySaveWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase5Rc8CustomerOnlySaveWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CustomerOnly_W1W2_Save_SetsLastCompletedPrintableTicket()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var workflow = new RecordingPrintWorkflow();
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync(workflow, new RecordingPrintNotificationService());
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Khách rc8 in";
            scale.SetManualWeightKg(18000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(9000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
            await vm.ExecuteMainPrintAsync();
        });

        Assert.Equal(1, workflow.PrintInvocationCount);
        Assert.Equal(vm.Tickets[0].Id, workflow.LastTicketId);
    }

    [Fact]
    public async Task CustomerOnly_W1W2_Save_CompletedAndResetsForm()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Khách rc8 reset";
            scale.SetManualWeightKg(18000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(9000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
        });

        Assert.Single(vm.Tickets);
        Assert.Equal(TicketFormMode.Creating, vm.FormMode);
        Assert.Null(vm.CustomerName);
        Assert.Null(vm.LicensePlate);
        Assert.Null(vm.CargoTypeName);
        Assert.False(vm.IsSaving);
    }
}

public sealed class Phase5Rc8SaveExceptionHandlingTests
{
    [Fact]
    public void SaveException_ConsumeSaveErrorHandled_SuppressesDuplicatePopup()
    {
        AppExceptionLogger.MarkSaveErrorHandled();
        Assert.True(AppExceptionLogger.ConsumeSaveErrorHandled());
        Assert.False(AppExceptionLogger.ConsumeSaveErrorHandled());
    }

    [Fact]
    public void SaveException_LogsErrorsLog()
    {
        var path = AppExceptionLogger.ErrorsLogPath;
        if (File.Exists(path))
            File.Delete(path);

        AppExceptionLogger.WriteError("SaveAsync", new InvalidOperationException("rc8 save test"), "context=rc8");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("SaveAsync", text, StringComparison.Ordinal);
        Assert.Contains("rc8 save test", text, StringComparison.Ordinal);
    }
}
