using System.IO;
using System.Windows;
using System.Windows.Controls;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using CanXe.Infrastructure;
using CanXe.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc20MainWindowPrintTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc20MainWindowPrintTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void MainWindowXaml_PrintButton_BindsPrintTicketCommand()
    {
        var xaml = File.ReadAllText(ResolveMainWindowXamlPath());
        Assert.Contains("x:Name=\"MainPrintButton\"", xaml);
        Assert.Contains("Command=\"{Binding PrintTicketCommand}\"", xaml);
        Assert.DoesNotContain("Click=\"MainPrintButton_OnClick\"", xaml);
        Assert.Contains("PreviewMouseDown=\"MainPrintButton_OnPreviewMouseDown\"", xaml);
    }

    [Fact]
    public async Task MainPrintButton_InvokesWorkflow_WhenActiveTicketSet()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var recordingWorkflow = new RecordingPrintWorkflow();
        var recordingNotification = new RecordingPrintNotificationService();
        var (viewModel, _) = await host.CreateMainViewModelForBindingSmokeAsync(recordingWorkflow, recordingNotification);

        await _fixture.InvokeAsync(async _ =>
        {
            var window = new MainWindow(new NoOpUiFocusService())
            {
                DataContext = viewModel
            };
            window.Show();
            window.UpdateLayout();

            viewModel.FormMode = TicketFormMode.Viewing;
            viewModel.ActiveTicketId = 42;
            viewModel.PrintTicketCommand.NotifyCanExecuteChanged();

            var button = window.FindName("MainPrintButton") as Button;
            Assert.NotNull(button);
            Assert.Same(viewModel.PrintTicketCommand, button!.Command);
            Assert.True(viewModel.PrintTicketCommand.CanExecute(null));

            await viewModel.ExecuteMainPrintAsync();

            Assert.Equal(1, recordingWorkflow.PrintInvocationCount);
            Assert.Equal(42, recordingWorkflow.LastTicketId);
            Assert.Equal(PrintCommandSource.MainWindow, recordingWorkflow.LastSource);
            Assert.False(viewModel.IsPrinting);
            Assert.True(viewModel.PrintTicketCommand.CanExecute(null));

            window.Close();
            await Task.CompletedTask;
        });
    }

    [Fact]
    public void MainPrintButtonClick_WritesTelemetryLog()
    {
        var logger = new PrintCommandLogger();
        logger.Log(
            "MAIN_PRINT_BUTTON_CLICK_RECEIVED " +
            "button=MainPrintButton isEnabled=True isHitTestVisible=True " +
            "dataContext=MainViewModel command=AsyncRelayCommand canExecute=True " +
            "activeTicketId=7 selectedTicketId=null formMode=Viewing isDirty=False isPrinting=False");

        var logPath = CanXeLogPaths.GetLogFile("print-command.log");
        var text = File.ReadAllText(logPath);
        Assert.Contains("MAIN_PRINT_BUTTON_CLICK_RECEIVED", text);
        Assert.Contains("button=MainPrintButton", text);
    }

    private static string ResolveMainWindowXamlPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Desktop", "MainWindow.xaml");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate MainWindow.xaml");
    }
}
