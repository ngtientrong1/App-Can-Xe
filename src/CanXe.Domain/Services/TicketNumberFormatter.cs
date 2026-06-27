namespace CanXe.Domain.Services;

public static class TicketNumberFormatter
{
    public static string FormatDisplayNumber(int sequenceNumber, int month) =>
        $"{sequenceNumber:D4}/{month:D2}";

    public static string FormatInternalCode(int year, int month, int sequenceNumber) =>
        $"{year:D4}{month:D2}-{sequenceNumber:D4}";
}
