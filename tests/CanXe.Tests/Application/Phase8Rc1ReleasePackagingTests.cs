using System.IO;
using System.Reflection;
using CanXe.Application.Configuration;
using CanXe.Infrastructure.Diagnostics;

namespace CanXe.Tests.Application;

public sealed class Phase8Rc1ReleasePackagingTests
{
    [Fact]
    public void Desktop_Version_Is_1_0_2()
    {
        var csproj = ResolveFile(Path.Combine("src", "CanXe.Desktop", "CanXe.Desktop.csproj"));
        var text = File.ReadAllText(csproj);
        Assert.Contains("<Version>1.0.7</Version>", text, StringComparison.Ordinal);
        Assert.Contains("<InformationalVersion>1.0.7</InformationalVersion>", text, StringComparison.Ordinal);
        Assert.Contains("Cân Xe Tiến Trọng", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildInfo_ShowsAppNameAndCleanVersion()
    {
        var info = new BuildInfoProvider(new AppSettings { DeviceMode = "Hardware" }).GetBuildInfo();
        Assert.Equal("Cân Xe Tiến Trọng", info.AppName);
        Assert.DoesNotContain("+", info.Version, StringComparison.Ordinal);
        var display = info.ToDisplayText();
        Assert.Contains("App: Cân Xe Tiến Trọng", display, StringComparison.Ordinal);
        Assert.Contains("Version:", display, StringComparison.Ordinal);
        Assert.Contains("Configuration:", display, StringComparison.Ordinal);
    }

    [Fact]
    public void InnoSetup_Script_PreservesUserDataAndTargetsPublish()
    {
        var iss = ResolveFile(Path.Combine("installer", "CanXeTienTrong.iss"));
        var text = File.ReadAllText(iss);
        Assert.Contains("#define MyAppVersion \"1.0.7\"", text, StringComparison.Ordinal);
        Assert.Contains("CanXeTienTrong-Setup-1.0.7", text, StringComparison.Ordinal);
        Assert.Contains(@"publish\win10-x64\*", text, StringComparison.Ordinal);
        Assert.Contains("DestDir: \"{app}\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DestDir: \"{localappdata}", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[UninstallDelete]", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[InstallDelete]", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleaseNotes_Exist()
    {
        var notes = ResolveFile(Path.Combine("releases", "ReleaseNotes-1.0.7.txt"));
        var text = File.ReadAllText(notes);
        Assert.Contains("Cân Xe Tiến Trọng 1.0.7", text, StringComparison.Ordinal);
        Assert.Contains("%LocalAppData%\\CanXe", text, StringComparison.Ordinal);
    }

    private static string ResolveFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
