using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.Views.Printing;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc19IdentityIconTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc19IdentityIconTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void IdentityIcons_PassRasterValidation()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var report = IdentityIconRasterValidator.Validate(copy);
            Assert.True(report.Passed, string.Join(Environment.NewLine, report.Lines));
            Assert.Equal(0, report.PersonIconDetachedPixelCount);
            Assert.Equal(0, report.VehicleIconDetachedPixelCount);
            Assert.False(report.PersonIconTouchesTileEdge);
            Assert.False(report.VehicleIconTouchesTileEdge);
        });
    }

    [Fact]
    public void IdentityIcons_ArePathGeometryNotText()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(
                PrintLayoutGeometryTestRunner.BuildSampleModelPublic());
            var personTile = copy.FindName("CustomerIconTile") as System.Windows.Controls.Border;
            var vehicleTile = copy.FindName("PlateIconTile") as System.Windows.Controls.Border;
            Assert.NotNull(personTile);
            Assert.NotNull(vehicleTile);
            Assert.NotNull(FindFirstPath(personTile!));
            Assert.NotNull(FindFirstPath(vehicleTile!));
        });
    }

    private static System.Windows.Shapes.Path? FindFirstPath(System.Windows.DependencyObject root)
    {
        if (root is System.Windows.Shapes.Path path)
            return path;
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (System.Windows.Media.VisualTreeHelper.GetChild(root, i) is System.Windows.DependencyObject child)
            {
                var found = FindFirstPath(child);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }
}
