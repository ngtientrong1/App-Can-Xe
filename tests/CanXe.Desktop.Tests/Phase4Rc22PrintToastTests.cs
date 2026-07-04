using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

public sealed class Phase4Rc22PrintToastTests
{
    [Fact]
    public async Task RecordingNotification_ShowSuccessToast_IncrementsOnce()
    {
        var notification = new RecordingPrintNotificationService();
        await notification.ShowSuccessToastAsync("Brother HL-B2180DW");
        Assert.Equal(1, notification.SuccessCount);
        Assert.Equal("Brother HL-B2180DW", notification.LastSuccessPrinter);
    }

    [Fact]
    public void Workflow_UsesToast_NotBlockingMessageBox_OnSuccess()
    {
        var notification = new RecordingPrintNotificationService();
        var submission = new StubPrintSubmissionService(
            WeighTicketPrintResult.Succeeded(1, "Brother HL-B2180DW"));
        var workflow = CreateWorkflow(notification, submission);
        var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();

        workflow.PrintModelAsync(model, PrintCommandSource.MainWindow).GetAwaiter().GetResult();

        Assert.Equal(1, notification.SuccessCount);
        Assert.Equal(0, notification.ErrorCount);
    }

    [Fact]
    public async Task Workflow_DoesNotShowSuccessToast_WhenSubmissionFails()
    {
        var notification = new RecordingPrintNotificationService();
        var submission = new StubPrintSubmissionService(
            WeighTicketPrintResult.Failed("Không tìm thấy máy in khả dụng."));
        var workflow = CreateWorkflow(notification, submission);
        var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();

        await workflow.PrintModelAsync(model, PrintCommandSource.PreviewWindow);

        Assert.Equal(0, notification.SuccessCount);
        Assert.Equal(1, notification.ErrorCount);
    }

    [Fact]
    public void DefaultToastDuration_Is2500Ms()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(2500), WpfPrintNotificationService.DefaultSuccessToastDuration);
    }

    [Fact]
    public async Task MainViewModel_IsPrintingResets_IndependentOfToast()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var workflow = new RecordingPrintWorkflow
        {
            NextResult = WeighTicketPrintResult.Succeeded(1, "Brother HL-B2180DW")
        };
        var notification = new RecordingPrintNotificationService();
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync(workflow, notification);

        vm.ActiveTicketId = 12;
        await vm.ExecuteMainPrintAsync();

        Assert.False(vm.IsPrinting);
    }

    private static WeighTicketPrintWorkflow CreateWorkflow(
        IPrintNotificationService notification,
        IWeighTicketPrintSubmissionService submission) =>
        new(
            weighTicketService: null!,
            stationSettings: null!,
            printSettings: null!,
            submissionService: submission,
            documentFactory: new WpfWeighTicketDocumentFactory(),
            notificationService: notification,
            commandLogger: new PrintCommandLogger());

    private sealed class StubPrintSubmissionService(WeighTicketPrintResult result) : IWeighTicketPrintSubmissionService
    {
        public Task<WeighTicketPrintResult> PrintModelAsync(
            WeighTicketPrintModel model,
            PrintCommandSource source,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
