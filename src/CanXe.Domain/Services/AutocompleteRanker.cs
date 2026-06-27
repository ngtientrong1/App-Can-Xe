namespace CanXe.Domain.Services;

public static class AutocompleteRanker
{
    public const int ScoreExact = 1000;
    public const int ScoreStartsWith = 800;
    public const int ScoreTokenPrefix = 600;
    public const int ScoreContains = 400;
    public const int ScoreRecentEmptySearch = 100;

    public static int ScoreMatch(string displayText, string searchTerm)
    {
        var normalizedDisplay = TextNormalizer.Normalize(displayText);
        var normalizedSearch = TextNormalizer.Normalize(searchTerm);

        if (normalizedSearch.Length == 0)
            return ScoreRecentEmptySearch;

        if (normalizedDisplay == normalizedSearch)
            return ScoreExact;

        if (normalizedDisplay.StartsWith(normalizedSearch, StringComparison.Ordinal))
            return ScoreStartsWith;

        if (ScoreTokenPrefixMatch(normalizedDisplay, normalizedSearch))
            return ScoreTokenPrefix;

        if (normalizedDisplay.Contains(normalizedSearch, StringComparison.Ordinal))
            return ScoreContains;

        return 0;
    }

    public static bool ScoreTokenPrefixMatch(string normalizedDisplay, string normalizedSearch)
    {
        var displayTokens = normalizedDisplay.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var searchTokens = normalizedSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (searchTokens.Length == 0 || displayTokens.Length < searchTokens.Length)
            return false;

        for (var i = 0; i < searchTokens.Length; i++)
        {
            if (!displayTokens[i].StartsWith(searchTokens[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public static IReadOnlyList<T> Rank<T>(
        IEnumerable<T> items,
        Func<T, string> getDisplayText,
        Func<T, DateTimeOffset?> getLastUsedAt,
        string searchTerm,
        int maxResults = 8)
    {
        var normalizedSearch = TextNormalizer.Normalize(searchTerm);
        var ranked = items
            .Select(item => (
                Item: item,
                Score: ScoreMatch(getDisplayText(item), searchTerm),
                LastUsed: getLastUsedAt(item)))
            .Where(x => normalizedSearch.Length == 0 ? true : x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.LastUsed ?? DateTimeOffset.MinValue)
            .Take(maxResults)
            .Select(x => x.Item)
            .ToList();

        return ranked;
    }
}
