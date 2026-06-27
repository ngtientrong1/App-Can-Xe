namespace CanXe.Domain.Services;

public static class AutocompleteDropDownPolicy
{
    public const int DefaultMaxVisibleItems = 6;

    public static bool ShouldOpen(
        bool isFocused,
        string? text,
        int itemCount,
        bool suppressDropDown,
        bool isWindowActive) =>
        !suppressDropDown &&
        isWindowActive &&
        isFocused &&
        itemCount > 0 &&
        !string.IsNullOrWhiteSpace(text);

    public static int LimitItemCount(int count, int maxItems = DefaultMaxVisibleItems) =>
        count <= 0 ? 0 : Math.Min(count, maxItems);
}
