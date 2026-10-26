using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public sealed class WeighCanvasSizeTests
{
    private const double DesignWidth = 2209;
    private const double DesignHeight = 1090;

    [Fact]
    public void HostAtDesignAspect_ReturnsDesignSize()
    {
        var (w, h) = OperatorLayoutMetrics.GetWeighCanvasSize(DesignWidth, DesignHeight);

        Assert.Equal(DesignWidth, w, 1);
        Assert.Equal(DesignHeight, h, 1);
    }

    [Fact]
    public void UnmeasuredHost_FallsBackToDesignSize()
    {
        Assert.Equal((DesignWidth, DesignHeight), OperatorLayoutMetrics.GetWeighCanvasSize(0, 0));
    }

    [Theory]
    [InlineData(1536, 690)]   // 16:9 laptop at 125% scaling, a little wider than the design
    [InlineData(1904, 935)]   // maximized 1920x1080
    [InlineData(1920, 880)]
    public void HostNearDesignAspect_FillsHostWithoutLetterbox(double hostW, double hostH)
    {
        var (w, h) = OperatorLayoutMetrics.GetWeighCanvasSize(hostW, hostH);

        Assert.Equal(hostW / hostH, w / h, 3);
        Assert.True(w >= DesignWidth);
        Assert.InRange(h, 980, 1300);
    }

    [Fact]
    public void UltrawideHost_WidensCanvasOnceHeightLimitIsReached()
    {
        var (w, h) = OperatorLayoutMetrics.GetWeighCanvasSize(3424, 1300);

        Assert.Equal(980, h, 1);
        Assert.Equal(3424.0 / 1300.0, w / h, 3);
        Assert.True(w > DesignWidth);
    }

    [Fact]
    public void NarrowHost_NeverShrinksBelowDesignWidthOrAboveMaxHeight()
    {
        var (w, h) = OperatorLayoutMetrics.GetWeighCanvasSize(1280, 900);

        Assert.True(w >= DesignWidth);
        Assert.True(h <= 1300);
    }
}
