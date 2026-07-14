using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

public sealed class Phase4Rc20PrintPopupTests
{
    [Fact]
    public void Workflow_ShowsSuccessPopup_AfterSuccessfulSubmission()
    {
        var notification = new RecordingPrintNotificationService();
        var submission = new StubPrintSubmissionService(
            WeighTicketPrintResult.Succeeded(1, "Brother HL-B2180DW"));
        var workflow = CreateWorkflow(notification, submission);
        var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();

        workflow.PrintModelAsync(model, PrintCommandSource.MainWindow).GetAwaiter().GetResult();

        Assert.Equal(1, notification.SuccessCount);
        Assert.Equal("Brother HL-B2180DW", notification.LastSuccessPrinter);
        Assert.Equal(0, notification.ErrorCount);
        Assert.Equal(1, submission.InvocationCount);
    }

    [Fact]
    public void Workflow_ShowsErrorPopup_WhenSubmissionFails()
    {
        var notification = new RecordingPrintNotificationService();
        var submission = new StubPrintSubmissionService(
            WeighTicketPrintResult.Failed("Không tìm thấy máy in khả dụng."));
        var workflow = CreateWorkflow(notification, submission);
        var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();

        workflow.PrintModelAsync(model, PrintCommandSource.PreviewWindow).GetAwaiter().GetResult();

        Assert.Equal(0, notification.SuccessCount);
        Assert.Equal(1, notification.ErrorCount);
        Assert.Contains("máy in", notification.LastErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MainViewModel_ShowsError_WhenWorkflowModelBuildFails()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var workflow = new RecordingPrintWorkflow { FailModelBuild = true };
        var notification = new RecordingPrintNotificationService();
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync(workflow, notification);

        vm.FormMode = TicketFormMode.Viewing;
        vm.ActiveTicketId = 12;
        await vm.ExecuteMainPrintAsync();

        Assert.Equal(0, notification.SuccessCount);
        Assert.Contains("Không thể dựng", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
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
        public int InvocationCount { get; private set; }

        public Task<WeighTicketPrintResult> PrintModelAsync(
            WeighTicketPrintModel model,
            PrintCommandSource source,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(result);
        }
    }
}
