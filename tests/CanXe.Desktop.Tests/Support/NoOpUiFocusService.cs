namespace CanXe.Desktop.Tests.Support;

using CanXe.Desktop.Services;

public sealed class NoOpUiFocusService : IUiFocusService
{
    public void FocusCustomerField() { }

    public void FocusVehicleField() { }

    public void FocusCargoTypeField() { }

    public void FocusUnitPriceField() { }

    public void FocusNotesField() { }

    public void HighlightTicketRow(int ticketId, bool scrollToTop) { }
}
