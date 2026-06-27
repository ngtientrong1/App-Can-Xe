using System.Text;

using System.Text.Json;

using CanXe.DeviceTester.Core.Guided;
using CanXe.DeviceTester.Core.Models;



namespace CanXe.DeviceTester.Core.Services;



public sealed class RawBinaryCaptureWriter

{

    private readonly MemoryStream _stream = new();



    public void Reset() => _stream.SetLength(0);



    public void Append(byte[] data)

    {

        if (data.Length == 0)

            return;

        _stream.Write(data, 0, data.Length);

    }



    public byte[] GetBytes() => _stream.ToArray();



    public async Task<string?> SaveAsync(string basePathWithoutExtension, CancellationToken cancellationToken = default)

    {

        if (_stream.Length == 0)

            return null;



        var path = basePathWithoutExtension + ".raw.bin";

        await File.WriteAllBytesAsync(path, _stream.ToArray(), cancellationToken);

        return path;

    }



    public int TotalBytes => (int)_stream.Length;

}



public static class SessionMetadataWriter

{

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };



    public static async Task<string> WriteAsync(

        string basePathWithoutExtension,

        SerialCaptureSession session,

        int totalBytes,

        string? textLogPath,

        string? rawBinaryPath,

        CaptureSaveOptions options,

        CancellationToken cancellationToken = default)

    {

        var startedAt = ResolveStartedAt(session, options);
        var endedAt = ResolveEndedAt(options);
        var durationMs = (long)Math.Max(0, (endedAt - startedAt).TotalMilliseconds);

        var doc = new SessionMetadataDocument
        {
            SchemaVersion = 2,
            PortName = session.PortSettings.PortName,
            BaudRate = session.PortSettings.BaudRate,
            DataBits = session.PortSettings.DataBits,
            Parity = session.PortSettings.Parity.ToString(),
            StopBits = session.PortSettings.StopBits.ToString(),
            Handshake = session.PortSettings.Handshake.ToString(),
            Encoding = session.PortSettings.TextEncoding.ToString(),
            SessionType = session.SessionType?.ToString() ?? options.SessionType?.ToString(),
            SessionLabel = session.SessionLabel,
            KnownWeightKg = session.KnownWeightKg,
            KnownWeightIsApproximate = session.KnownWeightIsApproximate,
            IsStableSession = session.IsStableSession ?? false,
            StartedAt = startedAt.ToString("O"),
            EndedAt = endedAt.ToString("O"),
            DurationMilliseconds = durationMs,

            TotalBytes = totalBytes,

            TotalChunks = options.TotalChunks,

            UniqueByteValues = (options.UniqueByteValues ?? []).Select(b => b.ToString("X2")).ToList(),

            TextLogFile = textLogPath is null ? null : Path.GetFileName(textLogPath),

            RawBinaryFile = rawBinaryPath is null ? null : Path.GetFileName(rawBinaryPath),

            Events = (options.Events ?? []).Select(e => new CaptureEventMarkerDto

            {

                EventType = e.EventType,

                Label = e.Label,

                Timestamp = e.Timestamp.ToString("O"),

                ElapsedMilliseconds = e.ElapsedMilliseconds,

                RawByteOffset = e.RawByteOffset,

                ChunkNumber = e.ChunkNumber

            }).ToList()

        };



        var path = basePathWithoutExtension + ".session.json";

        var json = JsonSerializer.Serialize(doc, JsonOptions);

        await File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);

        return path;

    }

    private static DateTimeOffset ResolveStartedAt(SerialCaptureSession session, CaptureSaveOptions options)
    {
        var marker = options.Events?
            .FirstOrDefault(e => string.Equals(e.EventType, CaptureEventTypes.CaptureStarted, StringComparison.Ordinal));
        if (marker is not null)
            return marker.Timestamp;
        if (options.RecordingStartedAt is not null)
            return options.RecordingStartedAt.Value;
        if (session.StartedAt != default)
            return session.StartedAt;
        return DateTimeOffset.Now;
    }

    private static DateTimeOffset ResolveEndedAt(CaptureSaveOptions options)
    {
        var marker = options.Events?
            .LastOrDefault(e => string.Equals(e.EventType, CaptureEventTypes.CaptureStopped, StringComparison.Ordinal));
        if (marker is not null)
            return marker.Timestamp;
        if (options.RecordingEndedAt is not null)
            return options.RecordingEndedAt.Value;
        return DateTimeOffset.Now;
    }

}

