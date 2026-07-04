using CanXe.Application.Models;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.Views.Printing;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc23A4TwoUpTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc23A4TwoUpTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void CreateDocument_ProducesTwoIndependentCopies()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic(PrintLayoutMode.A4TwoUp);
            var document = new WpfWeighTicketDocumentFactory().CreateA4TwoUpDocument(model);
            var page = document.Pages[0].GetPageRoot(false) as FixedPage;
            var copies = page?.Children.OfType<WeighTicketCopyView>().ToList() ?? [];
            Assert.Equal(2, copies.Count);
            Assert.NotSame(copies[0], copies[1]);
        });
    }

    [Fact]
    public void ProductionCreateDocument_UsesA4TwoUp()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic(PrintLayoutMode.A5SingleTicket);
            var document = new WpfWeighTicketDocumentFactory().CreateDocument(model);
            var page = document.Pages[0].GetPageRoot(false) as FixedPage;
            Assert.Equal(2, page?.Children.OfType<WeighTicketCopyView>().Count());
        });
    }
}
