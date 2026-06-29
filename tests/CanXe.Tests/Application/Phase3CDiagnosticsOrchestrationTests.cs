using CanXe.Application.Models;
using CanXe.Infrastructure.Diagnostics;

namespace CanXe.Tests.Application;

public sealed class Phase3CDiagnosticsOrchestrationTests
{
    [Fact]
    public void DiagnosticsTimeouts_MatchHardwareAcceptanceBudgets()
    {
        Assert.Equal(TimeSpan.FromSeconds(90), DiagnosticsTimeouts.Overall);
        Assert.Equal(TimeSpan.FromSeconds(15), DiagnosticsTimeouts.Database);
        Assert.Equal(TimeSpan.FromSeconds(30), DiagnosticsTimeouts.Camera);
        Assert.Equal(TimeSpan.FromSeconds(20), DiagnosticsTimeouts.Scale);
        Assert.Equal(TimeSpan.FromSeconds(10), DiagnosticsTimeouts.Cleanup);
    }

    [Fact]
    public void DatabaseOnly_DoesNotIncludeCameraOrScale()
    {
        var cli = DiagnosticsArgumentParser.Parse(["--database"]);
        var options = DiagnosticsArgumentParser.BuildRunOptions(cli, null);

        Assert.True(options.IncludeDatabase);
        Assert.False(options.IncludeApplication);
        Assert.False(options.IncludeFfmpeg);
        Assert.False(options.IncludeCameraConnection);
        Assert.False(options.IncludeScale);
    }

    [Fact]
    public void AllWithRequireFlags_IncludesSequentialPhases()
    {
        var cli = DiagnosticsArgumentParser.Parse(["--all", "--require-camera", "--require-scale"]);
        var options = DiagnosticsArgumentParser.BuildRunOptions(cli, @"C:\media\camera-loop.mp4");

        Assert.True(options.IncludeApplication);
        Assert.True(options.IncludeDatabase);
        Assert.True(options.IncludeFfmpeg);
        Assert.True(options.IncludeCameraConfig);
        Assert.True(options.IncludeCameraConnection);
        Assert.True(options.IncludeScale);
        Assert.True(options.RequireCamera);
        Assert.True(options.RequireScale);
        Assert.False(options.UseTestMedia);
    }

    [Fact]
    public void ProbeException_StillEvaluatesExitCode()
    {
        var result = new SystemDiagnosticsResult
        {
            Checks =
            [
                new SystemDiagnosticCheck
                {
                    Category = "Camera",
                    Name = "Phase timeout",
                    Status = DiagnosticStatus.Fail,
                    Detail = "Camera checks timeout after 30s"
                }
            ]
        };
        var options = new DiagnosticsRunOptions { RequireCamera = true, RequireScale = true };
        var summary = DiagnosticsExitEvaluator.Evaluate(result, options);
        Assert.Equal(1, summary.ExitCode);
        Assert.Equal("FAIL", summary.CameraResult);
    }

    [Fact]
    public void OverallTimeoutSummary_ReturnsExitCode1()
    {
        var result = new SystemDiagnosticsResult { Checks = [] };
        var options = new DiagnosticsRunOptions { RequireCamera = true, RequireScale = true };
        var summary = DiagnosticsExitEvaluator.Evaluate(result, options);
        Assert.Equal(1, summary.ExitCode);
        Assert.Equal("FAIL", summary.CameraResult);
        Assert.Equal("FAIL", summary.ScaleResult);
    }

    [Fact]
    public void RequiredScaleFail_ReturnsExitCode1()
    {
        var result = new SystemDiagnosticsResult
        {
            Checks =
            [
                new SystemDiagnosticCheck
                {
                    Category = "Scale",
                    Name = "Valid frame in timeout",
                    Status = DiagnosticStatus.Fail,
                    Detail = "timeout"
                }
            ]
        };
        var summary = DiagnosticsExitEvaluator.Evaluate(result, new DiagnosticsRunOptions { RequireScale = true });
        Assert.Equal(1, summary.ExitCode);
        Assert.Equal("FAIL", summary.ScaleResult);
    }

    [Fact]
    public void ScaleSerialSettings_MapsFromDto()
    {
        var dto = new ScaleDeviceSettingsDto
        {
            PortName = "COM1",
            BaudRate = 1200,
            DataBits = 8,
            Parity = "None",
            StopBits = "One",
            Handshake = "None"
        };

        var settings = ScaleHardwareProbe.ToSerialSettings(dto);
        Assert.Equal("COM1", settings.PortName);
        Assert.Equal(1200, settings.BaudRate);
    }
}
