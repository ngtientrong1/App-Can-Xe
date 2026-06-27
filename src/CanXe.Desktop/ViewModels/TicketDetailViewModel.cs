using System.Windows;
using CanXe.Application.Models;

namespace CanXe.Desktop.ViewModels;

public sealed class TicketDetailViewModel
{
    public TicketDetailViewModel(WeighTicketDetailDto detail) => Detail = detail;

    public WeighTicketDetailDto Detail { get; }

    public string WindowTitle => $"Chi tiết phiếu {Detail.DisplayNumber}";

    public static void Show(Window owner, WeighTicketDetailDto detail)
    {
        var window = new TicketDetailWindow
        {
            Owner = owner,
            DataContext = new TicketDetailViewModel(detail)
        };
        window.ShowDialog();
    }
}
