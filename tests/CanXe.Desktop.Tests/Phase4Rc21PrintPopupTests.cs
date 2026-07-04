using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

public sealed class Phase4Rc21PrintPopupTests
{
    [Fact]
    public void Notification_ShowsLayoutValidationMessage_WithoutGenericPrinterPrefix()
    {
        var notification = new RecordingPrintNotificationService();
        notification.ShowPrintError(PrintA5AdaptiveFitOutcome.ValidationFailureMessage);

        Assert.Equal(1, notification.ErrorCount);
        Assert.Contains("chưa thể thu gọn", notification.LastErrorMessage ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("kiểm tra máy in", notification.LastErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Workflow_ShowsSuccessPopup_OnlyAfterSpoolCompletion()
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
    public async Task MainViewModel_IsPrinting_ResetAfterValidationFailure()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var workflow = new RecordingPrintWorkflow
        {
            NextResult = WeighTicketPrintResult.Failed(PrintA5AdaptiveFitOutcome.ValidationFailureMessage)
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
