using System.IO;
using CanXe.Desktop.Services;
using CanXe.Infrastructure.Logging;

namespace CanXe.Desktop.Tests;

public sealed class Phase4Rc19PrintCommandLoggerTests
{
    [Fact]
    public void PrintCommandLogger_WritesToExpectedPath()
    {
        var logger = new PrintCommandLogger();
        logger.Log("rc19-test-entry previewDependency=false");
        var path = CanXeLogPaths.GetLogFile("print-command.log");
        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("rc19-test-entry", content);
    }
}
