using CanXe.Application.Models;

namespace CanXe.Desktop.ViewModels;

public sealed class MainPrintButtonTelemetry
{
    public required string ButtonName { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsHitTestVisible { get; init; }
    public string DataContextType { get; init; } = "null";
    public string CommandType { get; init; } = "null";
    public bool CanExecute { get; init; }
    public int? ActiveTicketId { get; init; }
    public int? SelectedTicketId { get; init; }
    public TicketFormMode FormMode { get; init; }
    public bool IsDirty { get; init; }
    public bool IsPrinting { get; init; }
}
