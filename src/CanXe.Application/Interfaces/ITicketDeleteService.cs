namespace CanXe.Application.Interfaces;

public sealed class TicketDeleteResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? TicketId { get; init; }
    public string? DisplayNumber { get; init; }

    public static TicketDeleteResult Succeeded(int ticketId, string displayNumber) =>
        new() { Success = true, TicketId = ticketId, DisplayNumber = displayNumber };

    public static TicketDeleteResult Failed(string message) =>
        new() { Success = false, ErrorMessage = message };

    public static TicketDeleteResult Cancelled() =>
        new() { Success = false, ErrorMessage = "cancelled" };
}

public interface ITicketDeleteService
{
    Task<TicketDeleteResult> SoftDeleteAsync(
        int ticketId,
        string? deleteReason = null,
        CancellationToken cancellationToken = default);
}
