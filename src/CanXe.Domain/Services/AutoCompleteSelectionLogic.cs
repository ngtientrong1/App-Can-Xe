namespace CanXe.Domain.Services;

public static class AutoCompleteSelectionLogic
{
    public static int MoveHighlight(int currentIndex, int itemCount, int direction)
    {
        if (itemCount <= 0)
            return -1;

        if (currentIndex < 0)
            return direction >= 0 ? 0 : itemCount - 1;

        var next = currentIndex + direction;
        if (next < 0)
            return itemCount - 1;

        if (next >= itemCount)
            return 0;

        return next;
    }

    public static string? ResolveCommitText(
        string? typedText,
        object? selectedItem,
        int highlightedIndex,
        IReadOnlyList<string> items)
    {
        if (highlightedIndex >= 0 && highlightedIndex < items.Count)
            return items[highlightedIndex];

        if (selectedItem is string selectedText && !string.IsNullOrEmpty(selectedText))
            return selectedText;

        return typedText;
    }

    public static bool HasHighlightedSelection(int highlightedIndex, int itemCount) =>
        highlightedIndex >= 0 && highlightedIndex < itemCount;
}
