using System.Text;

using CanXe.Application.Models;
using CanXe.Infrastructure.Logging;

namespace CanXe.Infrastructure.Camera;

public static class CameraConnectionLogger
{

    public static string LogFilePath => CanXeLogPaths.GetLogFile("camera.log");

    public static void WriteSessionHeader(
        int operationId,
        string version,
        string appDirectory,
        string deviceMode,
        string decoderName,
        string? ffmpegPath,
        string? ffmpegVersion)
    {
        Write(
            operationId,
            "SessionStart",
            decoder: decoderName,
            snapshot: $"version={version}; appDir={appDirectory}; mode={deviceMode}; ffmpeg={ffmpegPath ?? "missing"}; ffmpegVersion={ffmpegVersion ?? "unknown"}");
    }

    public static void Write(
        int operationId,
        string eventName,
        string? sanitizedEndpoint = null,
        string? transport = null,
        string? decoder = null,
        string? codec = null,
        string? resolution = null,
        string? authentication = null,
        DateTimeOffset? firstPacketAt = null,
        DateTimeOffset? firstDecodedFrameAt = null,
        DateTimeOffset? firstRenderedFrameAt = null,
        int? framesReceived = null,
        int? framesDecoded = null,
        int? framesRendered = null,
        double? lastFrameAgeSeconds = null,
        string? snapshot = null,
        int? exitCode = null,
        string? error = null)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
            builder.AppendLine($"Event: {eventName}");
            builder.AppendLine($"OperationId: {operationId}");
            if (!string.IsNullOrWhiteSpace(sanitizedEndpoint))
                builder.AppendLine($"Sanitized endpoint: {sanitizedEndpoint}");
            if (!string.IsNullOrWhiteSpace(transport))
                builder.AppendLine($"Transport: {transport}");
            if (!string.IsNullOrWhiteSpace(decoder))
                builder.AppendLine($"Decoder: {decoder}");
            if (!string.IsNullOrWhiteSpace(authentication))
                builder.AppendLine($"Authentication: {authentication}");
            if (!string.IsNullOrWhiteSpace(codec))
                builder.AppendLine($"Codec: {codec}");
            if (!string.IsNullOrWhiteSpace(resolution))
                builder.AppendLine($"Resolution: {resolution}");
            if (firstPacketAt.HasValue)
                builder.AppendLine($"First packet time: {firstPacketAt.Value:O}");
            if (firstDecodedFrameAt.HasValue)
                builder.AppendLine($"First decoded frame time: {firstDecodedFrameAt.Value:O}");
            if (firstRenderedFrameAt.HasValue)
                builder.AppendLine($"First rendered frame time: {firstRenderedFrameAt.Value:O}");
            if (framesReceived.HasValue)
                builder.AppendLine($"Frames received: {framesReceived.Value}");
            if (framesDecoded.HasValue)
                builder.AppendLine($"Frames decoded: {framesDecoded.Value}");
            if (framesRendered.HasValue)
                builder.AppendLine($"Frames rendered: {framesRendered.Value}");
            if (lastFrameAgeSeconds.HasValue)
                builder.AppendLine($"Last frame age: {lastFrameAgeSeconds.Value:F2}s");
            if (!string.IsNullOrWhiteSpace(snapshot))
                builder.AppendLine($"Snapshot: {snapshot}");
            if (exitCode.HasValue)
                builder.AppendLine($"Decoder exit code: {exitCode.Value}");
            if (!string.IsNullOrWhiteSpace(error))
                builder.AppendLine($"Error: {error}");
            builder.AppendLine(new string('-', 60));

            SafeLogFileAppend.Append(LogFilePath, builder.ToString());
        }
        catch (Exception ex)
        {
            try
            {
                StartupErrorLoggerFallback.Write($"Camera logger failed: {ex.Message}");
            }
            catch
            {
                // Best effort only.
            }
        }
    }

    public static void WriteStateChange(
        int operationId,
        CameraConnectionState previousState,
        CameraConnectionState newState,
        string reason,
        Guid sessionId,
        int? ffmpegPid,
        bool processAlive,
        int framesDecoded,
        DateTimeOffset? lastValidFrameAt,
        TimeSpan? lastFrameAge)
    {
        Write(
            operationId,
            "StateChanged",
            framesDecoded: framesDecoded,
            lastFrameAgeSeconds: lastFrameAge?.TotalSeconds,
            firstRenderedFrameAt: lastValidFrameAt,
            snapshot:
                $"PreviousState={previousState}; NewState={newState}; Reason={reason}; " +
                $"SessionId={sessionId}; FfmpegPid={ffmpegPid?.ToString() ?? "null"}; " +
                $"ProcessAlive={processAlive}; StallDetected={newState == CameraConnectionState.Stalled}");
    }

    public static void WriteReconnectRequested(
        int operationId,
        string reason,
        int reconnectAttempt,
        Guid sessionId,
        int? ffmpegPid)
    {
        Write(
            operationId,
            "ReconnectRequested",
            snapshot:
                $"Reason={reason}; ReconnectAttempt={reconnectAttempt}; SessionId={sessionId}; PID={ffmpegPid?.ToString() ?? "null"}");
    }

    internal static class StartupErrorLoggerFallback
    {
        public static void Write(string message)
        {
            SafeLogFileAppend.AppendLine(
                CanXeLogPaths.GetLogFile("startup-error.log"),
                $"{DateTimeOffset.Now:O} {message}");
        }
    }
}
