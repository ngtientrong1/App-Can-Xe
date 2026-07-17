using System.Collections.Concurrent;
using System.Text;

namespace CanXe.Infrastructure.Logging;

/// <summary>
/// Best-effort shared log append with per-path locking and FileShare.ReadWrite.
/// Replaces File.AppendAllText which opens files exclusively and fails when
/// multiple dotnet test assemblies write scale-mode.log concurrently.
/// </summary>
public static class SafeLogFileAppend
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Append(string path, string text, Encoding? encoding = null)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var semaphore = Locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        semaphore.Wait();
        try
        {
            encoding ??= Encoding.UTF8;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var stream = new FileStream(
                        path,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var writer = new StreamWriter(stream, encoding);
                    writer.Write(text);
                    return;
                }
                catch (IOException) when (attempt < 2)
                {
                    Thread.Sleep(50 + attempt * 50);
                }
            }

            // Diagnostic loggers must never crash callers (e.g. InitializeAsync in tests).
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static void AppendLine(string path, string line, Encoding? encoding = null) =>
        Append(path, line + Environment.NewLine, encoding);

    public static void AppendLines(string path, IEnumerable<string> lines, Encoding? encoding = null)
    {
        var text = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        Append(path, text, encoding);
    }
}
