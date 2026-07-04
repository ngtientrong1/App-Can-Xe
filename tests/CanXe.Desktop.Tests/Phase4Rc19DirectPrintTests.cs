using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using CanXe.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc19DirectPrintTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc19DirectPrintTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void PrintCommand_IsRelayCommandWithCanExecute()
    {
        var method = typeof(MainViewModel).GetMethod("CanPrintTicket",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
    }

    [Fact]
    public async Task BuildPrintModelAsync_LoadsFromTicketId()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        await DependencyInjection.InitializeDatabaseAsync(host.Provider, host.AppPaths.DatabasePath);
        var service = host.Provider.GetRequiredService<WeighTicketService>();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        var capture1 = await service.CaptureWeightAsync(draft, 1);
        Assert.True(capture1.Success, capture1.ErrorMessage);
        scale.SetManualWeightKg(18500m);
        var capture2 = await service.CaptureWeightAsync(draft, 2);
        Assert.True(capture2.Success, capture2.ErrorMessage);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);

        var model = await host.Provider.GetRequiredService<IWeighTicketPrintWorkflow>()
            .BuildPrintModelAsync(save.SavedTicket!.Id);
        Assert.NotNull(model);
        Assert.Equal(save.SavedTicket.Id, model!.TicketId);
    }

    [Fact]
    public void Workflow_CreatesFreshDocumentWithoutPreview()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var factory = new WpfWeighTicketDocumentFactory();
            var doc1 = factory.CreateDocument(model);
            var doc2 = factory.CreateDocument(model);
            Assert.NotSame(doc1, doc2);
            var page1 = doc1.Pages[0].GetPageRoot(false);
            var page2 = doc2.Pages[0].GetPageRoot(false);
            Assert.NotSame(page1, page2);
        });
    }
}
