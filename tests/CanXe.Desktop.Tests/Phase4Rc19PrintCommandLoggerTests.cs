using System.IO;
using CanXe.Desktop.Services;

namespace CanXe.Desktop.Tests;

public sealed class Phase4Rc19PrintCommandLoggerTests
{
    [Fact]
    public void PrintCommandLogger_WritesToExpectedPath()
    {
        var logger = new PrintCommandLogger();
        logger.Log("rc19-test-entry previewDependency=false");
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe",
            "Logs",
            "print-command.log");
        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("rc19-test-entry", content);
    }
}
