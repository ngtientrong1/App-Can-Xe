using CanXe.Application.Models;
using CanXe.Infrastructure.Diagnostics;

namespace CanXe.Tests.Application;

public class Phase3CHardwareDiagnosticsTests
{
    [Fact]
    public void RequireCamera_DoesNotUseTestMedia()
    {
        var cli = DiagnosticsArgumentParser.Parse(["--all", "--require-camera"]);
        var options = DiagnosticsArgumentParser.BuildRunOptions(cli, @"C:\tests\TestAssets\camera-loop.mp4");

        Assert.True(options.RequireCamera);
        Assert.False(options.UseTestMedia);
        Assert.Null(options.TestMediaPath);
    }

    [Fact]
    public void PublishCheck_UsesTestMedia()
    {
        var cli = DiagnosticsArgumentParser.Parse(["--publish-check"]);
        var media = @"C:\tests\TestAssets\camera-loop.mp4";
        var options = DiagnosticsArgumentParser.BuildRunOptions(cli, media);

        Assert.True(options.UseTestMedia);
        Assert.Equal(media, options.TestMediaPath);
        Assert.False(options.RequireCamera);
    }

    [Fact]
    public void SkipCamera_SkipsCameraSections()
    {
        var cli = DiagnosticsArgumentParser.Parse(["--all", "--skip-camera"]);
        var options = DiagnosticsArgumentParser.BuildRunOptions(cli, @"C:\media\camera-loop.mp4");

        Assert.True(options.SkipCamera);
        Assert.False(options.RequireCamera);
        Assert.False(options.IncludeCameraConfig);
        Assert.False(options.IncludeCameraConnection);
    }

    [Fact]
    public void SkipScale_SkipsScaleSection()
    {
        var cli = DiagnosticsArgumentParser.Parse(["--all", "--skip-scale"]);
        var options = DiagnosticsArgumentParser.BuildRunOptions(cli, null);

        Assert.True(options.SkipScale);
        Assert.False(options.RequireScale);
        Assert.False(options.IncludeScale);
    }

    [Fact]
    public void AllWithRequireFlags_KeepsBothRequired()
    {
        var cli = DiagnosticsArgumentParser.Parse(["--all", "--require-camera", "--require-scale"]);
        var options = DiagnosticsArgumentParser.BuildRunOptions(cli, @"C:\media\camera-loop.mp4");

        Assert.True(options.RequireCamera);
        Assert.True(options.RequireScale);
        Assert.False(options.UseTestMedia);
    }

    [Fact]
    public void ConflictingCameraFlags_Detected()
    {
        var cli = DiagnosticsArgumentParser.Parse(["--require-camera", "--skip-camera"]);
        Assert.True(cli.HasCameraFlagConflict);
    }

    [Fact]
    public void ConflictingScaleFlags_Detected()
    {
        var cli = DiagnosticsArgumentParser.Parse(["--require-scale", "--skip-scale"]);
        Assert.True(cli.HasScaleFlagConflict);
    }

    [Fact]
    public void RequireCameraUnavailable_EvaluatesFail()
    {
        var result = new SystemDiagnosticsResult
        {
            Checks =
            [
                Check("Camera", "RTSP connect", DiagnosticStatus.Fail, "timeout")
            ]
        };
        var options = new DiagnosticsRunOptions { RequireCamera = true };
        var summary = DiagnosticsExitEvaluator.Evaluate(result, options);

        Assert.Equal(1, summary.ExitCode);
        Assert.Equal("FAIL", summary.CameraResult);
    }

    [Fact]
    public void SkipCamera_EvaluatesSkipped()
    {
        var result = new SystemDiagnosticsResult { Checks = [] };
        var options = new DiagnosticsRunOptions { SkipCamera = true };
        var summary = DiagnosticsExitEvaluator.Evaluate(result, options);

        Assert.Equal("SKIPPED", summary.CameraResult);
        Assert.Equal(0, summary.ExitCode);
    }

    [Fact]
    public void RequireScaleSkippedFrame_EvaluatesFail()
    {
        var result = new SystemDiagnosticsResult
        {
            Checks =
            [
                Check("Scale", "COM port exists", DiagnosticStatus.Pass, "COM1"),
                Check("Scale", "Port open", DiagnosticStatus.Pass, "Opened"),
                Check("Scale", "Valid frame in timeout", DiagnosticStatus.Skipped, "NOT AVAILABLE")
            ]
        };
        var options = new DiagnosticsRunOptions { RequireScale = true };
        var summary = DiagnosticsExitEvaluator.Evaluate(result, options);

        Assert.Equal(1, summary.ExitCode);
        Assert.Equal("FAIL", summary.ScaleResult);
    }

    [Fact]
    public void RequireScaleValidFrame_EvaluatesPass()
    {
        var result = new SystemDiagnosticsResult
        {
            Checks =
            [
                Check("Scale", "COM port exists", DiagnosticStatus.Pass, "COM1"),
                Check("Scale", "Port open", DiagnosticStatus.Pass, "Opened"),
                Check("Scale", "Valid frame in timeout", DiagnosticStatus.Pass, "valid=1")
            ]
        };
        var options = new DiagnosticsRunOptions { RequireScale = true };
        var summary = DiagnosticsExitEvaluator.Evaluate(result, options);

        Assert.Equal(0, summary.ExitCode);
        Assert.Equal("PASS", summary.ScaleResult);
    }

    [Fact]
    public void CameraFail_ReturnsExitCode1()
    {
        var result = new SystemDiagnosticsResult
        {
            Checks =
            [
                Check("Camera", "RTSP connect", DiagnosticStatus.Fail, "No JPEG")
            ]
        };
        var options = new DiagnosticsRunOptions { RequireCamera = true, RequireScale = true };
        var summary = DiagnosticsExitEvaluator.Evaluate(result, options);
        Assert.Equal(1, summary.ExitCode);
    }

    [Fact]
    public void ScaleFail_ReturnsExitCode1()
    {
        var result = new SystemDiagnosticsResult
        {
            Checks =
            [
                Check("Scale", "Valid frame in timeout", DiagnosticStatus.Fail, "Không nhận được frame cân hợp lệ")
            ]
        };
        var options = new DiagnosticsRunOptions { RequireScale = true };
        var summary = DiagnosticsExitEvaluator.Evaluate(result, options);
        Assert.Equal(1, summary.ExitCode);
    }

    [Fact]
    public void RequiredCameraSkipCountsAsFail()
    {
        var result = new SystemDiagnosticsResult
        {
            Checks =
            [
                Check("Camera", "RTSP connect", DiagnosticStatus.Skipped, "NOT AVAILABLE")
            ]
        };
        var options = new DiagnosticsRunOptions { RequireCamera = true };
        var summary = DiagnosticsExitEvaluator.Evaluate(result, options);
        Assert.Equal("FAIL", summary.CameraResult);
        Assert.Equal(1, summary.ExitCode);
    }

    [Fact]
    public void OptionalLocalAcceptance_AllowsSkipScale()
    {
        var result = new SystemDiagnosticsResult
        {
            Checks =
            [
                Check("Scale", "Valid frame in timeout", DiagnosticStatus.Skipped, "NOT AVAILABLE")
            ]
        };
        var options = new DiagnosticsRunOptions { RequireScale = false, SkipScale = false };
        var summary = DiagnosticsExitEvaluator.Evaluate(result, options);
        Assert.Equal(0, summary.ExitCode);
        Assert.Equal("SKIP", summary.ScaleResult);
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
        Assert.Equal(8, settings.DataBits);
    }

    private static SystemDiagnosticCheck Check(string category, string name, DiagnosticStatus status, string detail) =>
        new()
        {
            Category = category,
            Name = name,
            Status = status,
            Detail = detail
        };
}
