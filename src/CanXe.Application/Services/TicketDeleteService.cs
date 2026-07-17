using CanXe.Application.Interfaces;

namespace CanXe.Application.Services;

public sealed class TicketDeleteService(
    IWeighTicketRepository ticketRepository,
    IDeveloperAuthorizationService developerAuthorization) : ITicketDeleteService
{
    public async Task<TicketDeleteResult> SoftDeleteAsync(
        int ticketId,
        string? deleteReason = null,
        CancellationToken cancellationToken = default)
    {
        if (!developerAuthorization.CanDeleteTickets)
        {
            TicketDeleteAuditWriter.Log(ticketId, null, "denied", "failed");
            return TicketDeleteResult.Failed("Bạn không có quyền xóa phiếu cân.");
        }

        var ticket = await ticketRepository.GetByIdWithEventsAsync(ticketId, cancellationToken).ConfigureAwait(false);
        if (ticket is null || ticket.IsDeleted)
        {
            TicketDeleteAuditWriter.Log(ticketId, ticket?.DisplayNumber, "granted", "failed");
            return TicketDeleteResult.Failed("Không tìm thấy phiếu cân.");
        }

        var deletedAt = DateTimeOffset.UtcNow;
        ticket.IsDeleted = true;
        ticket.DeletedAt = deletedAt;
        ticket.DeletedBy = "developer";
        ticket.DeleteReason = string.IsNullOrWhiteSpace(deleteReason) ? null : deleteReason.Trim();
        ticket.UpdatedAt = deletedAt;

        await ticketRepository.UpdateAsync(ticket, cancellationToken).ConfigureAwait(false);
        TicketDeleteAuditWriter.Log(ticketId, ticket.DisplayNumber, "granted", "completed", deletedAt);

        return TicketDeleteResult.Succeeded(ticketId, ticket.DisplayNumber);
    }
}

internal static class TicketDeleteAuditWriter
{
    private static readonly object Gate = new();

    public static void Log(
        int ticketId,
        string? ticketNumber,
        string authorizationResult,
        string status,
        DateTimeOffset? deletedAt = null,
        string? exception = null)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CanXe",
                "Logs");
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, "ticket-delete.log");
            var line =
                $"[{DateTimeOffset.Now:O}] ticketId={ticketId} ticketNumber={ticketNumber ?? "null"} " +
                $"authorization={authorizationResult} status={status} deletedAt={deletedAt?.ToString("O") ?? "null"}";
            if (!string.IsNullOrWhiteSpace(exception))
                line += $" exception={exception}";

            lock (Gate)
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        using var stream = new FileStream(
                            path,
                            FileMode.Append,
                            FileAccess.Write,
                            FileShare.ReadWrite | FileShare.Delete);
                        using var writer = new StreamWriter(stream);
                        writer.WriteLine(line);
                        return;
                    }
                    catch (IOException) when (attempt < 2)
                    {
                        Thread.Sleep(50 + attempt * 50);
                    }
                }
            }
        }
        catch
        {
            // diagnostics only
        }
    }
}
