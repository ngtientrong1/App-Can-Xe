using System.Windows.Controls;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc19SignatureCleanupTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc19SignatureCleanupTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void SignatureSection_HasNoHintText()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(
                PrintLayoutGeometryTestRunner.BuildSampleModelPublic());
            Assert.Null(FindTextBlock(copy, "(Ký và ghi rõ họ tên)"));
            Assert.NotNull(FindTextBlock(copy, "LÁI XE"));
            Assert.NotNull(FindTextBlock(copy, "CHỦ HÀNG"));
            Assert.NotNull(FindTextBlock(copy, "NGƯỜI CÂN"));
        });
    }

    [Fact]
    public void SignatureSection_HeightUnchanged()
    {
        Assert.Equal(26, WeighTicketPrintLayout.SignatureSectionHeightMm);
    }

    [Fact]
    public void SignatureRoles_AreCentered()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(
                PrintLayoutGeometryTestRunner.BuildSampleModelPublic());
            foreach (var label in new[] { "LÁI XE", "CHỦ HÀNG", "NGƯỜI CÂN" })
            {
                var text = FindTextBlock(copy, label);
                Assert.NotNull(text);
                Assert.Equal(System.Windows.TextAlignment.Center, text!.TextAlignment);
                Assert.Equal(System.Windows.FontWeights.SemiBold, text.FontWeight);
            }
        });
    }

    private static TextBlock? FindTextBlock(System.Windows.DependencyObject root, string text)
    {
        if (root is TextBlock tb && tb.Text == text)
            return tb;
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (System.Windows.Media.VisualTreeHelper.GetChild(root, i) is System.Windows.DependencyObject child)
            {
                var found = FindTextBlock(child, text);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }
}
