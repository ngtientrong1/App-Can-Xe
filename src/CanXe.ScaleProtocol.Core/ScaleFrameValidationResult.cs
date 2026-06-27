namespace CanXe.ScaleProtocol.Core;

public sealed class ScaleFrameValidationResult
{
    public bool IsValid { get; init; }
    public ScaleProtocolFrame? Frame { get; init; }
    public string? ErrorCode { get; init; }

    public static ScaleFrameValidationResult Valid(ScaleProtocolFrame frame) =>
        new() { IsValid = true, Frame = frame };

    public static ScaleFrameValidationResult Invalid(string errorCode) =>
        new() { IsValid = false, ErrorCode = errorCode };
}
