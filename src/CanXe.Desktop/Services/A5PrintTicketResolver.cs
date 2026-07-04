using System.Printing;

namespace CanXe.Desktop.Services;

public sealed class A5ValidatedPrintTicket
{
    public PrintTicket ValidatedTicket { get; init; } = null!;
    public PageOrientation RequestedOrientation { get; init; }
    public PageOrientation? ValidatedOrientation { get; init; }
    public ValidationResult ValidationResult { get; init; }
}

public static class A5PrintTicketResolver
{
    public static A5ValidatedPrintTicket MergeAndValidateA5Landscape(PrintQueue queue, int copies)
    {
        var requestedTicket = new PrintTicket
        {
            PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA5),
            PageOrientation = PageOrientation.Landscape,
            Duplexing = Duplexing.OneSided,
            CopyCount = copies
        };

        var baseTicket = queue.UserPrintTicket ?? queue.DefaultPrintTicket;
        var validation = queue.MergeAndValidatePrintTicket(baseTicket, requestedTicket);
        return new A5ValidatedPrintTicket
        {
            ValidatedTicket = validation.ValidatedPrintTicket,
            RequestedOrientation = PageOrientation.Landscape,
            ValidatedOrientation = validation.ValidatedPrintTicket.PageOrientation,
            ValidationResult = validation
        };
    }
}
