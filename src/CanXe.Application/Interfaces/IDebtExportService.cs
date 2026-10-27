namespace CanXe.Application.Interfaces;

public sealed record DebtExportResult(
    bool Success,
    string? ErrorMessage,
    string? FilePath = null,
    int TicketCount = 0,
    int DeletedCount = 0,
    string? StationCode = null,
    string? StationName = null)
{
    public static DebtExportResult Failed(string message) => new(false, message);
}

/// <summary>Writes the file the debt app imports: the completed tickets of this scale station in a date range.</summary>
public interface IDebtExportService
{
    Task<DebtExportResult> ExportAsync(
        DateTime fromDate,
        DateTime toDate,
        string filePath,
        CancellationToken cancellationToken = default);
}
