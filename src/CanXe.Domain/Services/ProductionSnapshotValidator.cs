namespace CanXe.Domain.Services;

public static class ProductionSnapshotValidator
{
    public const int WarningFileBytes = 10 * 1024;
    public const int AbsurdlySmallBytes = 512;
    public const int MinProductionWidth = 320;
    public const int MinProductionHeight = 180;

    private static readonly HashSet<string> KnownPlaceholderBase64Hashes =
    [
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
    ];

    public static SnapshotValidationResult Validate(
        long fileSize,
        int width,
        int height,
        bool decodable,
        bool looksLikeJpeg,
        bool isSolidColor = false,
        bool fileExists = true)
    {
        if (!fileExists)
            return SnapshotValidationResult.Fail("File does not exist");

        if (!decodable || !looksLikeJpeg)
            return SnapshotValidationResult.Fail("Decode failed or not a valid JPEG");

        if (width <= 1 || height <= 1)
            return SnapshotValidationResult.Fail("Image is 1×1 or smaller");

        if (width <= MinProductionWidth || height <= MinProductionHeight)
            return SnapshotValidationResult.Fail($"Dimensions too small ({width}×{height})");

        if (IsKnownPlaceholderSize(fileSize))
            return SnapshotValidationResult.Fail("Known placeholder size");

        if (isSolidColor)
            return SnapshotValidationResult.Fail("Image is nearly solid color");

        if (fileSize < AbsurdlySmallBytes)
            return SnapshotValidationResult.Fail($"Extremely small file ({fileSize} bytes)");

        if (fileSize < WarningFileBytes)
            return SnapshotValidationResult.Warning($"Valid JPEG but small ({fileSize / 1024.0:F1} KB)");

        return SnapshotValidationResult.Pass($"{fileSize / 1024.0:F1} KB, {width}×{height}");
    }

    public static bool IsKnownPlaceholder(byte[] bytes)
    {
        if (bytes.Length <= 128)
            return true;

        var b64 = Convert.ToBase64String(bytes);
        return KnownPlaceholderBase64Hashes.Contains(b64);
    }

    private static bool IsKnownPlaceholderSize(long fileSize) => fileSize <= 128;

    [Obsolete("Use Validate() for severity-aware checks.")]
    public static bool IsProductionValid(long fileSize, int width, int height, bool decodable, bool isSolidColor = false) =>
        Validate(fileSize, width, height, decodable, decodable, isSolidColor).IsPass;
}
