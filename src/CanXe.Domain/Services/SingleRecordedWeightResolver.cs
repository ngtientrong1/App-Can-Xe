namespace CanXe.Domain.Services;

public static class SingleRecordedWeightResolver
{
    public static decimal? Resolve(int eventCount, int? singleEventWeightGrams)
    {
        if (eventCount != 1 || singleEventWeightGrams is null)
            return null;

        return singleEventWeightGrams.Value / 1000m;
    }
}
