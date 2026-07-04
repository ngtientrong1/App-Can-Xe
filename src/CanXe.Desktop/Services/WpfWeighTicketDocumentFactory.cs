using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop.Views.Printing;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public sealed class WpfWeighTicketDocumentFactory : IWeighTicketDocumentFactory
{
    public FixedDocument CreateDocument(WeighTicketPrintModel model) =>
        CreateA4TwoUpDocument(model);

    public FixedDocument CreateA5SingleDocument(WeighTicketPrintModel model)
    {
        var document = new FixedDocument();
        var page = new FixedPage
        {
            Width = WeighTicketPrintLayout.A5LandscapePageWidthDip,
            Height = WeighTicketPrintLayout.A5LandscapePageHeightDip,
            Background = WeighTicketPrintResources.PageBackground
        };

        var copy = CreateMaterializedCopy(model);
        FixedPage.SetLeft(copy, 0);
        FixedPage.SetTop(copy, 0);
        page.Children.Add(copy);

        var content = new PageContent();
        ((IAddChild)content).AddChild(page);
        document.Pages.Add(content);
        return document;
    }

    public FixedDocument CreateA4TwoUpDocument(WeighTicketPrintModel model)
    {
        var document = new FixedDocument();
        var page = new FixedPage
        {
            Width = WeighTicketPrintLayout.PageWidthDip,
            Height = WeighTicketPrintLayout.PageHeightDip,
            Background = WeighTicketPrintResources.PageBackground
        };

        var topCopy = CreateMaterializedCopy(model);
        var bottomCopy = CreateMaterializedCopy(model);

        AddCopyToPage(page, topCopy, WeighTicketPrintLayout.TopCopyTopDip);
        AddCopyToPage(page, bottomCopy, WeighTicketPrintLayout.BottomCopyTopDip);

        var content = new PageContent();
        ((IAddChild)content).AddChild(page);
        document.Pages.Add(content);
        return document;
    }

    public static WeighTicketCopyView CreateMaterializedCopy(WeighTicketPrintModel model)
    {
        var copy = new WeighTicketCopyView
        {
            Background = WeighTicketPrintResources.PageBackground
        };
        copy.Bind(model);
        MaterializeTicket(copy);
        return copy;
    }

    public static void MaterializeTicket(FrameworkElement element)
    {
        var width = WeighTicketPrintLayout.TicketLogicalWidthDip;
        var height = WeighTicketPrintLayout.TicketLogicalHeightDip;
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    public static void Materialize(FrameworkElement element)
    {
        if (element is WeighTicketCopyView)
        {
            MaterializeTicket(element);
            return;
        }

        var width = element.Width > 0 ? element.Width : WeighTicketPrintLayout.PageWidthDip;
        var height = element.Height > 0 ? element.Height : WeighTicketPrintLayout.PageHeightDip;
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    public static int CountDescendants(DependencyObject root)
    {
        var count = 1;
        var children = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < children; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
                count += CountDescendants(child);
        }
        return count;
    }

    private static void AddCopyToPage(FixedPage page, WeighTicketCopyView copy, double topDip)
    {
        FixedPage.SetLeft(copy, 0);
        FixedPage.SetTop(copy, topDip);
        page.Children.Add(copy);
        MaterializeTicket(copy);
    }
}

public sealed class WpfWeighTicketDocumentBuilder(IWeighTicketDocumentFactory factory) : IWeighTicketDocumentBuilder
{
    public IDocumentPageSource BuildA4TwoUpDocument(WeighTicketPrintModel model)
    {
        var document = factory.CreateDocument(model);
        return new WpfDocumentPageSource(document, model.PrintSettings.PrintLayoutMode);
    }

    [Obsolete("Use IWeighTicketDocumentFactory.CreateDocument on UI thread.")]
    public static FixedDocument BuildFixedDocument(WeighTicketPrintModel model) =>
        new WpfWeighTicketDocumentFactory().CreateDocument(model);
}
