namespace CanXe.Application.Services;

public static class TicketListCollectionPolicy
{
    public static bool TreatSavedTicketAsNewListEntry(bool wasContinuationSave) => !wasContinuationSave;

    public static int CompareStableOrder(
        DateTimeOffset leftTicketDateTime,
        int leftId,
        DateTimeOffset rightTicketDateTime,
        int rightId)
    {
        var dateCompare = leftTicketDateTime.CompareTo(rightTicketDateTime);
        return dateCompare != 0 ? dateCompare : leftId.CompareTo(rightId);
    }

    public static int FindInsertIndex<T>(
        IReadOnlyList<T> items,
        T savedItem,
        Func<T, DateTimeOffset> getTicketDateTime,
        Func<T, int> getId)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (CompareStableOrder(
                    getTicketDateTime(savedItem),
                    getId(savedItem),
                    getTicketDateTime(items[i]),
                    getId(items[i])) > 0)
            {
                return i;
            }
        }

        return items.Count;
    }
}
