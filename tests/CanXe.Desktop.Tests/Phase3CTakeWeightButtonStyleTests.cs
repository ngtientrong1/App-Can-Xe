using System.Windows;
using System.Windows.Controls;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase3CTakeWeightButtonStyleTests(WpfSmokeFixture wpf)
{
    [Theory]
    [InlineData("WeighActionButtonStyle")]
    [InlineData("PrimaryActionButtonStyle")]
    [InlineData("SecondaryActionButtonStyle")]
    [InlineData("DangerActionButtonStyle")]
    [InlineData("CanXeButtonBase")]
    public void ActionButtonStyles_AreRegistered(string key)
    {
        wpf.Invoke(app => Assert.NotNull(app.Resources[key] as Style));
    }

    [Fact]
    public void ButtonBase_HasHoverPressedDisabledTriggers()
    {
        wpf.Invoke(app =>
        {
            var style = app.Resources["CanXeButtonBase"] as Style;
            var template = style!.Setters
                .OfType<Setter>()
                .First(s => s.Property == Control.TemplateProperty).Value as ControlTemplate;
            var triggers = template!.Triggers.OfType<Trigger>().Select(t => t.Property?.Name).ToList();
            Assert.Contains("IsMouseOver", triggers);
            Assert.Contains("IsPressed", triggers);
            Assert.Contains("IsEnabled", triggers);
        });
    }

    [Fact]
    public void RecordWeightButton_UsesWeighActionStyle()
    {
        wpf.Invoke(app =>
        {
            var style = app.Resources["RecordWeightButton"] as Style;
            Assert.Equal(app.Resources["WeighActionButtonStyle"], style!.BasedOn);
        });
    }

    [Fact]
    public void SaveButton_UsesPrimaryActionStyle()
    {
        wpf.Invoke(app =>
        {
            var style = app.Resources["SaveButton"] as Style;
            Assert.Equal(app.Resources["PrimaryActionButtonStyle"], style!.BasedOn);
        });
    }
}
