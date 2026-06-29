namespace CanXe.Infrastructure.Diagnostics;

public static class DiagnosticsTimeouts
{
    public static readonly TimeSpan Overall = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan Application = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan Database = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan Ffmpeg = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan Camera = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Scale = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan Cleanup = TimeSpan.FromSeconds(10);
}
