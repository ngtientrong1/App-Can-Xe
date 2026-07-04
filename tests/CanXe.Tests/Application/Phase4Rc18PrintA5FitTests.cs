using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using Xunit;

namespace CanXe.Tests.Application;

public sealed class Phase4Rc18PrintA5FitTests
{
    [Fact]
    public void A4TwoUp_IsDefaultPrintLayoutMode()
    {
        var settings = new PrintSettingsDto();
        Assert.Equal(PrintLayoutMode.A4TwoUp, settings.PrintLayoutMode);
    }

    [Fact]
    public void ProductionPolicy_NormalizesLegacyA5ToA4TwoUp()
    {
        var settings = new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A5SingleTicket };
        ProductionPrintLayoutPolicy.NormalizeForProduction(settings);
        Assert.Equal(PrintLayoutMode.A4TwoUp, settings.PrintLayoutMode);
    }

    [Fact]
    public void LogicalTicket_Uses210x148_5Mm()
    {
        Assert.Equal(210, WeighTicketPrintLayout.TicketLogicalWidthMm);
        Assert.Equal(148.5, WeighTicketPrintLayout.TicketLogicalHeightMm);
    }

    [Fact]
    public void SafeContentBox_Is190x137_5Mm()
    {
        Assert.Equal(190, WeighTicketPrintLayout.ContentWidthMm);
        Assert.Equal(137.5, WeighTicketPrintLayout.ContentHeightMm);
        Assert.Equal(10, WeighTicketPrintLayout.SafeContentLeftMm);
        Assert.Equal(10, WeighTicketPrintLayout.SafeContentRightMm);
        Assert.Equal(5.5, WeighTicketPrintLayout.SafeContentTopMm);
        Assert.Equal(5.5, WeighTicketPrintLayout.SafeContentBottomMm);
    }

    [Fact]
    public void PrinterSafetyInset_Is3mmHorizontal_2_5mmVertical()
    {
        Assert.Equal(3.0, WeighTicketPrintLayout.PrinterImageableSafetyInsetHorizontalMm);
        Assert.Equal(2.5, WeighTicketPrintLayout.PrinterImageableSafetyInsetVerticalMm);
    }

    [Fact]
    public void FinalScale_IsMinOfOneScaleXScaleY()
    {
        var contentW = WeighTicketPrintLayout.TicketLogicalWidthDip;
        var contentH = WeighTicketPrintLayout.TicketLogicalHeightDip;
        var fit = PrintA5FitCalculator.Compute(5, 5, 200, 140, contentW, contentH);
        Assert.Equal(Math.Min(1.0, Math.Min(fit.ScaleX, fit.ScaleY)), fit.FinalScale, 4);
        Assert.True(fit.FinalScale <= 1.0);
    }

    [Fact]
    public void FinalScale_IsUniform()
    {
        var contentW = WeighTicketPrintLayout.TicketLogicalWidthDip;
        var contentH = WeighTicketPrintLayout.TicketLogicalHeightDip;
        var fit = PrintA5FitCalculator.Compute(0, 0, 200, 140, contentW, contentH);
        Assert.Equal(fit.FinalScale, fit.FinalWidthDip / contentW, 4);
        Assert.Equal(fit.FinalScale, fit.FinalHeightDip / contentH, 4);
    }

    [Fact]
    public void Placement_IsCenteredInTarget()
    {
        var contentW = WeighTicketPrintLayout.TicketLogicalWidthDip;
        var contentH = WeighTicketPrintLayout.TicketLogicalHeightDip;
        var fit = PrintA5FitCalculator.Compute(2, 2, 196, 136, contentW, contentH);
        var leftGap = fit.TranslateXDip - fit.TargetLeftDip;
        var rightGap = fit.TargetLeftDip + fit.TargetWidthDip - (fit.TranslateXDip + fit.FinalWidthDip);
        Assert.Equal(leftGap, rightGap, 2);
    }

    [Fact]
    public void SinglePassMapping_IsTrue()
    {
        var fit = PrintA5FitCalculator.Compute(
            0,
            0,
            200,
            140,
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip);
        Assert.True(fit.SinglePassMapping);
    }

    [Fact]
    public void FallbackCapabilities_DoesNotCrop()
    {
        var fit = PrintA5FitCalculator.ComputeFallback(
            WeighTicketPrintLayout.A5LandscapePageWidthDip,
            WeighTicketPrintLayout.A5LandscapePageHeightDip,
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip);
        Assert.True(fit.CapabilitiesFallback);
        Assert.True(fit.FinalWidthDip <= fit.TargetWidthDip);
        Assert.True(fit.FinalHeightDip <= fit.TargetHeightDip);
    }

    [Fact]
    public void A5RasterPixelDimensions_Match300Dpi()
    {
        var expectedW = (int)Math.Round(210 / 25.4 * 300);
        var expectedH = (int)Math.Round(148.5 / 25.4 * 300);
        Assert.Equal(expectedW, (int)Math.Round(WeighTicketPrintLayout.TicketLogicalWidthMm / 25.4 * 300));
        Assert.Equal(expectedH, (int)Math.Round(WeighTicketPrintLayout.TicketLogicalHeightMm / 25.4 * 300));
    }

    [Fact]
    public void MinRasterEdgeClearance_Is20pxHorizontal_16pxVertical()
    {
        Assert.Equal(20, WeighTicketPrintLayout.MinRasterEdgeClearancePx);
        Assert.Equal(16, WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx);
    }

    [Fact]
    public void HeaderInsetConstants_MatchRc18Spec()
    {
        Assert.Equal(2.5, WeighTicketPrintLayout.HeaderLeftInsetLeftMm);
        Assert.Equal(2.0, WeighTicketPrintLayout.HeaderLeftInsetRightMm);
        Assert.Equal(1.5, WeighTicketPrintLayout.HeaderLeftInsetTopMm);
        Assert.Equal(1.5, WeighTicketPrintLayout.HeaderLeftInsetBottomMm);
        Assert.Equal(2.5, WeighTicketPrintLayout.HeaderRightMinInsetFromSafeRightMm);
    }

    [Fact]
    public void LayoutSectionsTotalHeight_MatchesContentHeight()
    {
        Assert.Equal(137.5, WeighTicketPrintLayout.LayoutSectionsTotalHeightMm, 1);
        Assert.Equal(WeighTicketPrintLayout.ContentHeightMm, WeighTicketPrintLayout.LayoutSectionsTotalHeightMm, 1);
    }

    [Fact]
    public void A5LandscapePage_Is210x148Mm()
    {
        Assert.Equal(210, WeighTicketPrintLayout.A5LandscapePageWidthMm);
        Assert.Equal(148, WeighTicketPrintLayout.A5LandscapePageHeightMm);
    }

    [Fact]
    public void InvalidTargetRect_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PrintA5FitCalculator.Compute(
                0,
                0,
                2,
                2,
                WeighTicketPrintLayout.TicketLogicalWidthDip,
                WeighTicketPrintLayout.TicketLogicalHeightDip));
    }
}
