namespace CanXe.Desktop.ViewModels;

public sealed class DiagnosticRowViewModel
{
    public required string Category { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
    public required string Detail { get; init; }
    public required string DurationText { get; init; }
}
