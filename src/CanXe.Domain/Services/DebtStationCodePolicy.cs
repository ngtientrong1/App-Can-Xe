namespace CanXe.Domain.Services;

/// <summary>The code that tells the debt app which scale station a ticket came from: A–Z, 0–9 and underscore.</summary>
public static class DebtStationCodePolicy
{
    public const int MinLength = 2;
    public const int MaxLength = 20;

    public static string? Normalize(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    public static bool IsValid(string? code) =>
        Normalize(code) is { } value
        && value.Length is >= MinLength and <= MaxLength
        && value.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');
}
