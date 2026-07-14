using System.IO;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase5Rc7PartialSaveCrashWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase5Rc7PartialSaveCrashWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CustomerOnly_Weight1Weight2_Save_DoesNotCrash()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Khách rc7";
            scale.SetManualWeightKg(18000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(9000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
        });

        Assert.Single(vm.Tickets);
        Assert.Equal("Khách rc7", vm.Tickets[0].CustomerName);
        Assert.Null(vm.Tickets[0].LicensePlate);
        Assert.Null(vm.Tickets[0].CargoTypeName);
        Assert.Equal(TicketFormMode.Creating, vm.FormMode);
        Assert.Null(vm.CustomerName);
        Assert.False(vm.IsSaving);
    }

    [Fact]
    public async Task CustomerOnly_Weight1Weight2_Save_WithWpfFocusService_DoesNotCrash()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        var focus = new WpfUiFocusService();

        await _fixture.InvokeAsync(async _ =>
        {
            var window = new MainWindow(focus)
            {
                DataContext = vm,
                Width = 1366,
                Height = 768
            };
            window.Show();
            window.UpdateLayout();

            vm.CustomerName = "Khách rc7 focus";
            scale.SetManualWeightKg(18000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(9000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);

            window.Close();
        });

        Assert.Single(vm.Tickets);
        Assert.Equal("Khách rc7 focus", vm.Tickets[0].CustomerName);
    }

    [Fact]
    public async Task CustomerOnly_Weight1Weight2_Save_SetsLastCompletedPrintableTicket()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var workflow = new RecordingPrintWorkflow();
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync(workflow, new RecordingPrintNotificationService());
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Khách in rc7";
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

    [Theory]
    [InlineData("Khách A", null, null)]
    [InlineData(null, "51A-12345", null)]
    [InlineData(null, null, "Gạo")]
    [InlineData(null, null, null)]
    public async Task PartialMetadata_Weight1Weight2_Save_DoesNotCrash(
        string? customer,
        string? plate,
        string? cargo)
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = customer;
            vm.LicensePlate = plate;
            vm.CargoTypeName = cargo;
            scale.SetManualWeightKg(15000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(6000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
        });

        Assert.Single(vm.Tickets);
        Assert.Equal(TicketFormMode.Creating, vm.FormMode);
    }
}

public sealed class Phase5Rc7SaveExceptionHandlingTests
{
    [Fact]
    public void AppExceptionLogger_WritesErrorsLog()
    {
        var path = AppExceptionLogger.ErrorsLogPath;
        if (File.Exists(path))
            File.Delete(path);

        AppExceptionLogger.WriteError("TestAction", new InvalidOperationException("rc7 test"), "context=test");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("TestAction", text, StringComparison.Ordinal);
        Assert.Contains("rc7 test", text, StringComparison.Ordinal);
    }
}
