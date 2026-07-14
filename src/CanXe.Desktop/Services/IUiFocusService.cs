namespace CanXe.Desktop.Services;

public interface IUiFocusService
{
    void FocusCustomerField();
    void FocusVehicleField();
    void FocusCargoTypeField();
    void FocusUnitPriceField();
    void FocusNotesField();
    void HighlightTicketRow(int ticketId, bool scrollToTop);

    void RefreshAutocompleteDisplays();
}
