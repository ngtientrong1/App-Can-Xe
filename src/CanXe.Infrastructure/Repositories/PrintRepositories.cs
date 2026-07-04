using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Entities;
using CanXe.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Infrastructure.Repositories;

public sealed class PrintSettingsRepository(CanXeDbContext db) : IPrintSettingsRepository
{
    public async Task<PrintSettingsDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await db.PrintSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task SaveAsync(PrintSettingsDto settings, CancellationToken cancellationToken = default)
    {
        var row = await db.PrintSettings.FirstOrDefaultAsync(cancellationToken)
            ?? new PrintSettings();

        row.PreferredPrinterName = settings.PreferredPrinterName;
        row.PaperSize = settings.PaperSize;
        row.PaperOrientation = settings.PaperOrientation;
        row.DefaultCopies = settings.DefaultCopies;
        row.TopCopyLabel = settings.TopCopyLabel;
        row.BottomCopyLabel = settings.BottomCopyLabel;
        row.ShowLogo = settings.ShowLogo;
        row.ShowPrice = settings.ShowPrice;
        row.TicketFooterText = settings.TicketFooterText;
        row.SignLocationName = settings.SignLocationName;
        row.ShowReprintWatermark = settings.ShowReprintWatermark;
        row.PrintRenderingMode = settings.PrintRenderingMode.ToString();
        row.PrintLayoutMode = settings.PrintLayoutMode.ToString();
        row.UpdatedAt = DateTimeOffset.UtcNow;

        if (row.Id == 0)
        {
            row.Id = 1;
            db.PrintSettings.Add(row);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static PrintSettingsDto Map(PrintSettings row) => new()
    {
        PreferredPrinterName = row.PreferredPrinterName,
        PaperSize = row.PaperSize,
        PaperOrientation = row.PaperOrientation,
        DefaultCopies = row.DefaultCopies,
        TopCopyLabel = row.TopCopyLabel,
        BottomCopyLabel = row.BottomCopyLabel,
        ShowLogo = row.ShowLogo,
        ShowPrice = row.ShowPrice,
        TicketFooterText = row.TicketFooterText,
        SignLocationName = row.SignLocationName,
        ShowReprintWatermark = row.ShowReprintWatermark,
        PrintRenderingMode = ParseRenderingMode(row.PrintRenderingMode),
        PrintLayoutMode = ParseLayoutMode(row.PrintLayoutMode)
    };

    private static PrintLayoutMode ParseLayoutMode(string? value)
    {
        var normalized = CanXe.Application.Services.ProductionPrintLayoutPolicy.NormalizeStoredLayoutMode(value);
        return Enum.TryParse<PrintLayoutMode>(normalized, true, out var mode)
            ? mode
            : PrintLayoutMode.A4TwoUp;
    }

    private static PrintRenderingMode ParseRenderingMode(string? value) =>
        Enum.TryParse<PrintRenderingMode>(value, true, out var mode)
            ? mode
            : PrintRenderingMode.RasterCompatibility;
}

public sealed class PrintJobHistoryRepository(CanXeDbContext db) : IPrintJobHistoryRepository
{
    public async Task<long> AddAsync(PrintJobHistoryDto job, CancellationToken cancellationToken = default)
    {
        var row = new PrintJobHistory
        {
            TicketId = job.TicketId,
            TicketNumber = job.TicketNumber,
            PrinterName = job.PrinterName,
            PaperSize = job.PaperSize,
            Copies = job.Copies,
            RequestedAt = job.RequestedAt,
            CompletedAt = job.CompletedAt,
            Status = Enum.TryParse<PrintJobStatus>(job.Status, true, out var status) ? status : PrintJobStatus.Prepared,
            ErrorMessage = job.ErrorMessage,
            IsReprint = job.IsReprint
        };
        db.PrintJobHistories.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    public async Task UpdateStatusAsync(long id, string status, string? errorMessage, CancellationToken cancellationToken = default)
    {
        var row = await db.PrintJobHistories.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (row is null)
            return;

        row.Status = Enum.TryParse<PrintJobStatus>(status, true, out var parsed) ? parsed : PrintJobStatus.Failed;
        row.ErrorMessage = errorMessage;
        row.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrintJobHistoryDto>> GetForTicketAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        var rows = await db.PrintJobHistories.AsNoTracking()
            .Where(h => h.TicketId == ticketId)
            .OrderByDescending(h => h.RequestedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    private static PrintJobHistoryDto Map(PrintJobHistory row) => new()
    {
        Id = row.Id,
        TicketId = row.TicketId,
        TicketNumber = row.TicketNumber,
        PrinterName = row.PrinterName,
        PaperSize = row.PaperSize,
        Copies = row.Copies,
        RequestedAt = row.RequestedAt,
        CompletedAt = row.CompletedAt,
        Status = row.Status.ToString(),
        ErrorMessage = row.ErrorMessage,
        IsReprint = row.IsReprint
    };
}
