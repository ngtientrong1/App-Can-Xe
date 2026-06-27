using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface ITicketDocumentRenderer
{
    TicketDocumentRenderResult RenderFront(WeighTicketDetailDto detail, TicketDocumentRenderOptions? options = null);

    TicketDocumentRenderResult RenderBack(WeighTicketDetailDto detail, TicketDocumentRenderOptions? options = null);

    TicketDocumentRenderResult RenderCombinedVertical(WeighTicketDetailDto detail, TicketDocumentRenderOptions? options = null);
}

public sealed class TicketDocumentRenderOptions
{
    public string ScaleSiteName { get; set; } = "Trạm cân CanXe";
    public string? OwnerName { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TicketFooterText { get; set; }
    public int WidthPx { get; set; } = 794;
    public int FrontHeightPx { get; set; } = 1123;
    public int BackHeightPx { get; set; } = 1123;
}

public sealed class TicketDocumentRenderResult
{
    public required byte[] PngBytes { get; init; }
    public required string SuggestedFileName { get; init; }
}
