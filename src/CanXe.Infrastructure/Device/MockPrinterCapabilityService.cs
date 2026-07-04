using CanXe.Application.Interfaces;

namespace CanXe.Infrastructure.Device;

public sealed class MockPrinterCapabilityService : IPrinterCapabilityService
{
    public IReadOnlyList<PrinterInfo> GetInstalledPrinters() =>
    [
        new PrinterInfo
        {
            Name = "Mock Printer",
            IsDefault = true,
            SupportsA4 = true,
            IsOnline = true
        }
    ];

    public PrinterInfo? GetDefaultPrinter() => GetInstalledPrinters().FirstOrDefault();

    public PrinterValidationResult ValidatePrinter(string? preferredPrinterName) =>
        PrinterValidationResult.Ok(new PrinterInfo
        {
            Name = preferredPrinterName ?? "Mock Printer",
            IsDefault = true,
            SupportsA4 = true,
            IsOnline = true
        });
}
