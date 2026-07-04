using System.Text;

using CanXe.Application.Models;

using CanXe.ScaleProtocol.Core;



namespace CanXe.Infrastructure.Logging;



public static class OperatorActionLogger

{

    private static readonly object Gate = new();

    private static string? _lastTakeWeightOperationId;

    private static string? _lastTakeWeightAction;

    private static DateTimeOffset? _lastTakeWeightAt;

    private static DateTimeOffset? _lastTakeWeightDecodedFrameAt;

    private static DateTimeOffset? _lastTakeWeightRenderedFrameAt;

    private static CameraConnectionState? _cameraStateAtLastTakeWeight;

    private static string? _snapshotOperationState;

    private static string? _takeWeightOperationState;



    public static string LogFilePath =>

        Path.Combine(

            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),

            "CanXe",

            "Logs",

            "operator-actions.log");



    public static string CreateOperationId(string action) =>

        $"{action}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Interlocked.Increment(ref _operationCounter):D3}";



    private static int _operationCounter;



    public static void Write(

        string operationId,

        string action,

        string eventName,

        string? snapshot = null,

        long? elapsedMilliseconds = null,

        Exception? exception = null)

    {

        try

        {

            var builder = new StringBuilder();

            builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");

            builder.AppendLine($"OperationId: {operationId}");

            builder.AppendLine($"Action: {action}");

            builder.AppendLine($"Event: {eventName}");

            builder.AppendLine($"UIThreadId: {Environment.CurrentManagedThreadId}");

            if (elapsedMilliseconds.HasValue)

                builder.AppendLine($"ElapsedMilliseconds: {elapsedMilliseconds.Value}");

            if (!string.IsNullOrWhiteSpace(snapshot))

                builder.AppendLine($"Snapshot: {snapshot}");

            if (exception is not null)

                builder.AppendLine($"Exception: {exception.Message}");

            builder.AppendLine(new string('-', 60));



            lock (Gate)

            {

                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);

                File.AppendAllText(LogFilePath, builder.ToString(), Encoding.UTF8);

            }

        }

        catch

        {

            // Best effort only.

        }

    }



    public static void RegisterTakeWeightContext(
        string operationId,
        string action,
        ScaleReading? scaleReading)
    {
        _lastTakeWeightOperationId = operationId;
        _lastTakeWeightAction = action;
        _lastTakeWeightAt = DateTimeOffset.UtcNow;
        _takeWeightOperationState = "CaptureStarted";

        var readingAge = scaleReading is null
            ? (TimeSpan?)null
            : DateTimeOffset.UtcNow - scaleReading.ReceivedAt.ToUniversalTime();

        Write(
            operationId,
            action,
            "TakeWeightContextRegistered",
            $"ScaleReadingAgeAtClick={readingAge?.TotalMilliseconds:F0}ms; ScaleStableAtClick={scaleReading?.IsStable}");
    }

    public static void RegisterTakeWeightContext(

        string operationId,

        string action,

        CameraConnectionState cameraState,

        ScaleReading? scaleReading,

        DateTimeOffset? lastDecodedFrameAt,

        TimeSpan? lastDecodedFrameAge,

        DateTimeOffset? lastRenderedFrameAt,

        int? ffmpegPid,

        Guid sessionId,

        long framesDecoded,

        int dispatcherPending)

    {

        _lastTakeWeightOperationId = operationId;

        _lastTakeWeightAction = action;

        _lastTakeWeightAt = DateTimeOffset.UtcNow;

        _lastTakeWeightDecodedFrameAt = lastDecodedFrameAt;

        _lastTakeWeightRenderedFrameAt = lastRenderedFrameAt;

        _cameraStateAtLastTakeWeight = cameraState;

        _takeWeightOperationState = "CaptureStarted";



        var readingAge = scaleReading is null

            ? (TimeSpan?)null

            : DateTimeOffset.UtcNow - scaleReading.ReceivedAt.ToUniversalTime();



        var renderedAgeSeconds = lastRenderedFrameAt is null
            ? (double?)null
            : (DateTimeOffset.UtcNow - lastRenderedFrameAt.Value).TotalSeconds;

        Write(

            operationId,

            action,

            "TakeWeightContextRegistered",

            $"CameraState={cameraState}; " +

            $"ScaleReadingAgeAtClick={readingAge?.TotalMilliseconds:F0}ms; " +

            $"ScaleStableAtClick={scaleReading?.IsStable}; " +

            $"ScaleStableSource={scaleReading?.StableSource}; " +

            $"ScaleRawFlag={scaleReading?.RawStableFlag}; " +

            $"CameraLastDecodedFrameAge={lastDecodedFrameAge?.TotalSeconds:F2}s; " +

            $"CameraLastRenderedFrameAge={renderedAgeSeconds?.ToString("F2") ?? "—"}s; " +

            $"DispatcherPending={dispatcherPending}; " +

            $"FfmpegPid={ffmpegPid}; SessionId={sessionId}; FramesDecoded={framesDecoded}");

    }



    public static void WriteTakeWeightUiUpdated(

        string operationId,

        string action,

        DateTimeOffset clickedAt,

        DateTimeOffset weightCapturedAt,

        DateTimeOffset uiUpdatedAt,

        ScaleReading? scaleReadingAtClick,

        long elapsedMilliseconds)

    {

        _takeWeightOperationState = "UiUpdated";

        Write(

            operationId,

            action,

            "UiUpdated",

            $"WeightCapturedAt={weightCapturedAt:O}; " +

            $"UiUpdatedAt={uiUpdatedAt:O}; " +

            $"ClickToBusy={(clickedAt - clickedAt).TotalMilliseconds:F0}ms; " +

            $"ClickToWeightCaptured={(weightCapturedAt - clickedAt).TotalMilliseconds:F0}ms; " +

            $"ClickToUiUpdated={(uiUpdatedAt - clickedAt).TotalMilliseconds:F0}ms; " +

            $"ScaleStableAtClick={scaleReadingAtClick?.IsStable}; " +

            $"ScaleStableSource={scaleReadingAtClick?.StableSource}",

            elapsedMilliseconds);

    }



    public static void WriteSnapshotRejected(string reason, TimeSpan? frameAge, CameraConnectionState state) =>

        Write(

            _lastTakeWeightOperationId ?? "unknown",

            "Snapshot",

            "SnapshotRejected",

            $"Reason={reason}; FrameAge={frameAge?.TotalSeconds:F2}s; CameraState={state}");



    public static void TryLogCameraReconnectCorrelation(

        CameraConnectionState newState,

        string reason,

        DateTimeOffset? lastDecodedFrameAt,

        TimeSpan? lastDecodedFrameAge,

        DateTimeOffset? lastRenderedFrameAt,

        int previewQueueLength,

        bool snapshotOperationInProgress)

    {

        if (newState is not (CameraConnectionState.Stalled or CameraConnectionState.Reconnecting))

            return;



        if (_lastTakeWeightAt is null || _lastTakeWeightOperationId is null)

            return;



        var sinceTakeWeight = DateTimeOffset.UtcNow - _lastTakeWeightAt.Value;

        if (sinceTakeWeight > TimeSpan.FromSeconds(15))

            return;



        var decodedAgeAtClick = _lastTakeWeightDecodedFrameAt.HasValue

            ? (_lastTakeWeightAt.Value - _lastTakeWeightDecodedFrameAt.Value).TotalSeconds

            : (double?)null;

        var renderedAgeAtClick = _lastTakeWeightRenderedFrameAt.HasValue

            ? (_lastTakeWeightAt.Value - _lastTakeWeightRenderedFrameAt.Value).TotalSeconds

            : (double?)null;

        var decodedNowStale = lastDecodedFrameAge > TimeSpan.FromSeconds(3);

        var renderedNowStale = lastRenderedFrameAt is null

            || DateTimeOffset.UtcNow - lastRenderedFrameAt.Value > TimeSpan.FromSeconds(3);

        var onlyUiStale = !decodedNowStale && renderedNowStale;



        Write(

            _lastTakeWeightOperationId,

            _lastTakeWeightAction ?? "TakeWeight",

            "PossibleTakeWeightCorrelation",

            $"RelatedTakeWeightOperationId={_lastTakeWeightOperationId}; " +

            $"TimeSinceTakeWeight={sinceTakeWeight.TotalSeconds:F2}s; " +

            $"WasLastDecodedFrameStale={decodedNowStale}; " +

            $"WasOnlyUiRenderedFrameStale={onlyUiStale}; " +

            $"PreviewQueueLength={previewQueueLength}; " +

            $"SnapshotOperationState={(snapshotOperationInProgress ? "InProgress" : _snapshotOperationState ?? "Idle")}; " +

            $"TakeWeightOperationState={_takeWeightOperationState ?? "Unknown"}; " +

            $"LastDecodedFrameAgeAtReconnect={lastDecodedFrameAge?.TotalSeconds:F2}s; " +

            $"DecodedAgeAtClick={decodedAgeAtClick:F2}s; " +

            $"RenderedAgeAtClick={renderedAgeAtClick:F2}s; " +

            $"FrameHealthyBeforeClick={_cameraStateAtLastTakeWeight == CameraConnectionState.Connected}; " +

            $"NewState={newState}; Reason={reason}; " +

            $"LastDecodedFrameAt={lastDecodedFrameAt:O}");

    }

    public static void WritePerformance(string action, string metrics)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:O} [{action}] {metrics}{Environment.NewLine}";
            lock (Gate)
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CanXe", "Logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "operator-performance.log"), line);
            }
        }
        catch
        {
            // Best-effort diagnostics only.
        }
    }

}


