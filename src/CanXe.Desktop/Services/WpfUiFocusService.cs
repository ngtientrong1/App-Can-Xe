using CanXe.Desktop.ViewModels;

namespace CanXe.Desktop.Services;

public sealed class WpfUiFocusService : IUiFocusService
{
    private MainWindow? _window;

    public void RegisterWindow(MainWindow window) => _window = window;

    public void FocusCustomerField() => _window?.FocusCustomerField();

    public void FocusVehicleField() => _window?.FocusVehicleField();

    public void FocusCargoTypeField() => _window?.FocusCargoTypeField();

    public void FocusUnitPriceField() => _window?.FocusUnitPriceField();

    public void FocusNotesField() => _window?.FocusNotesField();
}
