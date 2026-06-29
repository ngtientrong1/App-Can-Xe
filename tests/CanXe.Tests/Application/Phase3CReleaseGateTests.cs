using System.Text.Json;
using CanXe.Application.Configuration;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Camera;

namespace CanXe.Tests.Application;

public class Phase3CReleaseGateTests
{
    [Fact]
    public void ProductionConfig_IsHardware()
    {
        var json = ReadProductionConfig();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Hardware", doc.RootElement.GetProperty("DeviceMode").GetString());
    }

    [Fact]
    public void ProductionConfig_DeveloperModeDisabled()
    {
        var json = ReadProductionConfig();
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("DeveloperMode").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("ShowDeveloperPanel").GetBoolean());
    }

    [Fact]
    public void ProductionConfig_ResolvesFfmpegDecoder()
    {
        var factory = new CameraDecoderFactory(new AppSettings { DeviceMode = "Hardware" });
        Assert.IsType<FfmpegRtspDecoder>(factory.CreateDecoder());
    }

    [Fact]
    public void SimulationConfig_ResolvesFakeDecoder()
    {
        var factory = new CameraDecoderFactory(new AppSettings { DeviceMode = "Simulation" });
        Assert.IsType<FakeCameraDecoder>(factory.CreateDecoder());
    }

    [Fact]
    public void HardwareWithFakeDecoder_ThrowsViaGuard()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CameraDecoderGuard.EnsureValid("Hardware", new FakeCameraDecoder()));
    }

    [Fact]
    public void ProductionConfigPolicy_RejectsSimulationInJson()
    {
        var json = """{"DeviceMode":"Hardware","DeveloperMode":false,"ShowDeveloperPanel":false,"note":"Simulation"}""";
        Assert.False(ProductionConfigPolicy.IsValidProductionConfig("Hardware", false, false, json));
    }

    [Fact]
    public void ProductionConfigPolicy_RejectsStimeoutInJson()
    {
        var json = """{"DeviceMode":"Hardware","args":"-stimeout 5000000"}""";
        Assert.False(ProductionConfigPolicy.IsValidProductionConfig("Hardware", false, false, json));
    }

    [Fact]
    public void FfmpegArgs_DoNotContainStimeout()
    {
        var args = FfmpegRtspDecoder.BuildArguments(new CanXe.Application.Models.CameraRuntimeSettings { RtspHost = "10.0.0.1" });
        Assert.DoesNotContain("-stimeout", args, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildInfo_IncludesAppFolder()
    {
        var provider = new Infrastructure.Diagnostics.BuildInfoProvider(
            new AppSettings { DeviceMode = "Simulation" },
            new CameraDecoderFactory(new AppSettings { DeviceMode = "Simulation" }));
        var info = provider.GetBuildInfo();
        Assert.False(string.IsNullOrWhiteSpace(info.AppBaseDirectory));
        Assert.Equal("FakeCameraDecoder", info.CameraDecoderName);
    }

    [Fact]
    public void DiagnosticsSanitizer_RedactsCredential()
    {
        var sanitized = Infrastructure.Diagnostics.RtspCredentialSanitizer.Redact(
            "rtsp://admin:secret@192.168.1.50/live");
        Assert.Contains("rtsp://***@", sanitized);
        Assert.DoesNotContain("secret", sanitized);
    }

    [Fact]
    public void DiagnosticsJson_IsValidShape()
    {
        var payload = new
        {
            passed = 1,
            failed = 0,
            checks = new[] { new { Category = "App", Name = "Test", Status = "PASS", Detail = "ok" } }
        };
        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("checks", out _));
    }

    private static string ReadProductionConfig()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "CanXe.Desktop", "appsettings.Production.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "CanXe.Desktop", "appsettings.Production.json"))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        throw new FileNotFoundException("appsettings.Production.json not found", candidates[0]);
    }
}
