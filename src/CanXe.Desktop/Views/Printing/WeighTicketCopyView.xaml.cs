using System.Windows;
using System.Windows.Controls;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Views.Printing;

public partial class WeighTicketCopyView : UserControl
{
    public WeighTicketCopyView()
    {
        InitializeComponent();
    }

    public void Bind(WeighTicketPrintModel model)
    {
        var viewModel = WeighTicketCopyViewModel.From(model);
        DataContext = viewModel;
        ReprintWatermark.Visibility = viewModel.ShowReprintWatermark
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var width = double.IsPositiveInfinity(constraint.Width)
            ? WeighTicketPrintLayout.TicketLogicalWidthDip
            : Math.Min(constraint.Width, WeighTicketPrintLayout.TicketLogicalWidthDip);
        var height = double.IsPositiveInfinity(constraint.Height)
            ? WeighTicketPrintLayout.TicketLogicalHeightDip
            : Math.Min(constraint.Height, WeighTicketPrintLayout.TicketLogicalHeightDip);
        return base.MeasureOverride(new Size(width, height));
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var width = Math.Min(arrangeBounds.Width, WeighTicketPrintLayout.TicketLogicalWidthDip);
        var height = Math.Min(arrangeBounds.Height, WeighTicketPrintLayout.TicketLogicalHeightDip);
        return base.ArrangeOverride(new Size(width, height));
    }
}
