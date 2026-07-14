namespace CanXe.Application.Models;

public static class ReportDateRange
{
    public static (DateTimeOffset? FromInclusive, DateTimeOffset? ToExclusive) Normalize(ReportFilter filter)
    {
        DateTimeOffset? fromInclusive = null;
        if (filter.FromDate.HasValue)
        {
            var date = filter.FromDate.Value.DateTime.Date;
            fromInclusive = new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
        }

        DateTimeOffset? toExclusive = null;
        if (filter.ToDate.HasValue)
        {
            var nextDay = filter.ToDate.Value.DateTime.Date.AddDays(1);
            toExclusive = new DateTimeOffset(nextDay, TimeZoneInfo.Local.GetUtcOffset(nextDay));
        }

        return (fromInclusive, toExclusive);
    }

    public static bool MatchesTicketDate(
        DateTimeOffset ticketDateTime,
        DateTimeOffset? fromInclusive,
        DateTimeOffset? toExclusive)
    {
        if (fromInclusive.HasValue && ticketDateTime < fromInclusive.Value)
            return false;

        if (toExclusive.HasValue && ticketDateTime >= toExclusive.Value)
            return false;

        return true;
    }
}
