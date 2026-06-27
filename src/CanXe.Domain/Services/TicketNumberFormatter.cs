namespace CanXe.Domain.Services;

public static class TicketNumberFormatter
{
    public static string FormatDisplayNumber(int sequenceNumber, int month) =>
        $"{sequenceNumber:D2}/{month:D2}";

    public static string FormatInternalCode(int year, int month, int sequenceNumber) =>
        $"{year:D4}{month:D2}-{sequenceNumber:D4}";

    public static string ResolveDisplayNumber(int sequenceNumber, int ticketMonth) =>
        FormatDisplayNumber(sequenceNumber, ticketMonth);

    public static string ResolveDisplayNumber(int sequenceNumber, int ticketMonth, string? storedDisplayNumber)
    {
        if (sequenceNumber > 0 && ticketMonth is >= 1 and <= 12)
            return FormatDisplayNumber(sequenceNumber, ticketMonth);

        return NormalizeStoredDisplayNumber(storedDisplayNumber);
    }

    public static string NormalizeStoredDisplayNumber(string? storedDisplayNumber)
    {
        if (string.IsNullOrWhiteSpace(storedDisplayNumber))
            return "—";

        if (TryParseDisplayNumber(storedDisplayNumber, out var sequence, out var month))
            return FormatDisplayNumber(sequence, month);

        return storedDisplayNumber.Trim();
    }

    public static bool TryParseDisplayNumber(string displayNumber, out int sequence, out int month)
    {
        sequence = 0;
        month = 0;
        var parts = displayNumber.Split('/');
        if (parts.Length != 2)
            return false;

        return int.TryParse(parts[0], out sequence)
            && int.TryParse(parts[1], out month);
    }
}
