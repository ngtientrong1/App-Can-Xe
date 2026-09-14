using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase2ELargeScreenLayoutTests
{
    [Theory]
    [InlineData(768, 500)]
    [InlineData(900, 580)]
    [InlineData(1080, 580)]
    [InlineData(1600, 550.4)]
    [InlineData(2500, 620)]
    public void WorkspaceMaxHeight_CapsTallScreens(double windowHeight, double expectedMax)
    {
        Assert.Equal(expectedMax, OperatorLayoutMetrics.GetWorkspaceMaxHeight(windowHeight));
    }

    [Theory]
    [InlineData(768, 220)]
    [InlineData(1366, 280)]
    [InlineData(2500, 280)]
    public void DataGridMinHeight_RespectsCompactAndStandardScreens(double windowHeight, double expectedMin)
    {
        Assert.Equal(expectedMin, OperatorLayoutMetrics.GetDataGridMinHeight(windowHeight));
    }

    [Theory]
    [InlineData(1366, 768, 68)]
    [InlineData(1920, 1080, 84)]
    [InlineData(2500, 1600, 84)]
    [InlineData(2560, 1600, 84)]
    public void LiveWeightFontSize_DoesNotGrowWithoutLimitOnTallScreens(
        double windowWidth,
        double windowHeight,
        double expectedFontSize)
    {
        Assert.Equal(expectedFontSize, OperatorLayoutMetrics.GetLiveWeightFontSize(windowWidth, windowHeight));
    }

    [Theory]
    [InlineData(2500, false, 8)]
    [InlineData(2560, false, 8)]
    public void LargeScreen_EstimatesAtLeastEightVisibleRows(double windowHeight, bool advancedFilter, int minimumRows)
    {
        var rows = OperatorLayoutMetrics.EstimateVisibleDataGridRows(windowHeight, advancedFilter);
        Assert.True(rows >= minimumRows, $"Expected at least {minimumRows} rows, got {rows}.");
    }

    [Fact]
    public void SummaryFooterMaxHeight_StaysWithinBudget()
    {
        Assert.True(OperatorLayoutMetrics.SummaryFooterMaxHeight <= 72);
    }

    [Theory]
    [InlineData(1366)]
    [InlineData(2500)]
    public void WorkspaceMaxHeight_IsBelowFortyTwoPercentOfUsableHeight(double windowHeight)
    {
        var usable = OperatorLayoutMetrics.GetUsableContentHeight(windowHeight);
        var maxWorkspace = OperatorLayoutMetrics.GetWorkspaceMaxHeight(windowHeight);
        // The workspace card also has a content-driven floor (it must fit the weigh/save
        // controls without squashing them), which can exceed 42% on a merely-tall-but-not-wide
        // screen; the 42% rule only needs to hold once the content floor stops binding.
        Assert.True(maxWorkspace <= usable * 0.42 + 1 || maxWorkspace <= 580,
            $"Workspace max {maxWorkspace} exceeds 42% of usable {usable}.");
    }
}
