namespace CanXe.Domain.Services;

public static class WeighTicketPrintLayout
{
    public const double MmPerInch = 25.4;
    public const double Dpi = 96.0;
    public const double PointsPerInch = 72.0;

    public const double PageWidthMm = 210.0;
    public const double PageHeightMm = 297.0;
    public const double CopyHeightMm = 148.5;

    public const double TicketLogicalWidthMm = 210.0;
    public const double TicketLogicalHeightMm = 148.5;

    public const double A5LandscapePageWidthMm = 210.0;
    public const double A5LandscapePageHeightMm = 148.0;

    public const double SafeContentLeftMm = 10.0;
    public const double SafeContentRightMm = 10.0;
    public const double SafeContentTopMm = 5.5;
    public const double SafeContentBottomMm = 5.5;

    [Obsolete("rc18: safe box is inside template.")]
    public const double CopyPaddingLeftMm = 0.0;
    [Obsolete("rc18: safe box is inside template.")]
    public const double CopyPaddingRightMm = 0.0;
    [Obsolete("rc18: safe box is inside template.")]
    public const double CopyPaddingTopMm = 0.0;
    [Obsolete("rc18: safe box is inside template.")]
    public const double CopyPaddingBottomMm = 0.0;

    public const double ContentWidthMm = TicketLogicalWidthMm - SafeContentLeftMm - SafeContentRightMm;
    public const double ContentHeightMm = 137.5;

    public const double CopySafeLeftEdgeMm = SafeContentLeftMm;
    public const double CopySafeRightEdgeMm = TicketLogicalWidthMm - SafeContentRightMm;
    public const double CopySafeTopEdgeMm = SafeContentTopMm;
    public const double CopySafeBottomEdgeMm = TicketLogicalHeightMm - SafeContentBottomMm;

    [Obsolete("rc15: outer frame removed.")]
    public const double OuterFramePaddingMm = 0;
    [Obsolete("rc15: use ContentWidthMm.")]
    public const double InnerLayoutWidthMm = ContentWidthMm;
    [Obsolete("rc15: use ContentHeightMm.")]
    public const double InnerLayoutHeightMm = ContentHeightMm;

    [Obsolete("rc15: outer frame removed.")]
    public const double OuterFrameBorderThicknessDip = 0;
    [Obsolete("rc15: outer frame removed.")]
    public const double OuterFrameCornerRadiusMm = 0;

    public const double RowSeparatorThicknessDip = 0.4;
    public const double ColumnSeparatorThicknessDip = 0.45;
    public const double HeaderColumnSeparatorThicknessDip = 0.45;
    public const double SignatureColumnSeparatorDip = 0.45;
    public const double DashedSeparatorThicknessDip = 0.35;
    public const double DetailRowSeparatorThicknessDip = 0.35;

    public const double HeaderSectionHeightMm = 19.0;
    public const double TitleSectionHeightMm = 10.5;
    public const double WeightHeroSectionHeightMm = 26.0;
    public const double HeroBodyGapMm = 2.0;
    public const double BodySectionHeightMm = 52.0;
    public const double BodySignatureGapMm = 2.0;
    public const double SignatureSectionHeightMm = 26.0;

    public const double IdentityCardsHeightMm = 19.0;
    public const double IdentityDetailsGapMm = 2.5;
    public const double DetailsTableHeightMm = 30.5;
    public const double TimestampCardHeightMm = 45.0;
    public const double SignDateRowHeightMm = 7.0;

    public const double CardHostInsetMm = 0.4;

    public const double WeighBlock1HeightMm = 21.8;
    public static double TimestampCardInnerHeightMm => TimestampCardHeightMm - TimestampCardPaddingVerticalMm * 2;
    public static double WeighBlock2HeightMm => TimestampCardInnerHeightMm - WeighBlock1HeightMm - DipToMm(RowSeparatorThicknessDip);
    public const double SignatureBlankHeightMm = 20.0;

    public const double HeaderLeftColumnStar = 57.0;
    public const double HeaderRightColumnStar = 43.0;
    public const double BodyLeftColumnStar = 66.0;
    public const double BodyRightColumnStar = 34.0;
    public const double FieldLabelColumnStar = 34.0;
    public const double FieldValueColumnStar = 66.0;
    public const double WeightHeroColumnStar = 1.0;
    public const double IdentityCustomerColumnStar = 56.0;
    public const double IdentityVehicleColumnStar = 44.0;

    public const double PrinterImageableSafetyInsetHorizontalMm = 3.0;
    public const double PrinterImageableSafetyInsetVerticalMm = 2.5;

    public const double MinPlacementEdgeClearanceHorizontalMm = 2.0;
    public const double MinPlacementEdgeClearanceVerticalMm = 1.5;

    public const double MinRasterEdgeClearancePx = 20;
    public const double MinRasterVerticalEdgeClearancePx = 16;

    public const double PrintRasterDpi = 300.0;
    public const double RasterAntialiasTolerancePx = 2.0;
    public const double AutoFitReserve = 0.985;

    public static double RasterAntialiasToleranceDip => RasterAntialiasTolerancePx * Dpi / PrintRasterDpi;

    public const double HeaderLeftInsetLeftMm = 2.5;
    public const double HeaderLeftInsetRightMm = 2.0;
    public const double HeaderLeftInsetTopMm = 1.5;
    public const double HeaderLeftInsetBottomMm = 1.5;
    public const double HeaderRightMinInsetFromSafeRightMm = 2.5;

    public const double HeaderRightPaddingLeftMm = 3.0;
    public const double HeaderRightPaddingRightMm = 4.0;

    public const double WeightCardGapMm = 3.0;
    public const double IdentityCardGapMm = 3.0;
    public const double BodyLeftTimestampGapMm = 3.0;

    public const double IdentityIconTileWidthMm = 12.5;
    public const double IdentityIconGraphicSizeMm = 7.75;
    public const double IdentityIconSeparatorThicknessDip = 0.4;

    public const double IdentityValuePaddingLeftMm = 2.0;
    public const double IdentityValuePaddingRightMm = 2.0;
    public const double IdentityValuePaddingTopMm = 1.0;
    public const double IdentityValuePaddingBottomMm = 1.0;

    public const double DetailsTablePaddingLeftMm = 3.0;
    public const double DetailsTablePaddingRightMm = 4.0;
    public const double DetailsTablePaddingTopMm = 1.5;
    public const double DetailsTablePaddingBottomMm = 1.5;
    public const double DetailLabelInsetMm = 1.0;
    public const double DetailValueInsetMm = 1.5;
    public const double DetailRowSeparatorInsetMm = 1.5;

    public const double TimestampCardPaddingHorizontalMm = 2.0;
    public const double TimestampCardPaddingVerticalMm = 1.5;
    [Obsolete("rc15: use TimestampCardPaddingHorizontalMm.")]
    public const double TimestampCardPaddingMm = TimestampCardPaddingHorizontalMm;
    public const double TimestampIconColumnMm = 7.5;
    public const double TimestampIconSizeMm = 5.75;
    public const double TimestampIconStrokeThicknessDip = 1.7;

    public const double IdentityIconTileBorderInsetDip = 0.5;
    public const double CardBorderThicknessDip = 0.75;
    public const double CardCornerRadiusMm = 1.8;
    public const double WeightCardPaddingHorizontalMm = 1.5;
    public const double WeightCardPaddingVerticalMm = 1.5;
    [Obsolete("rc15: use TimestampIconSizeMm.")]
    public const double TimestampIconMaxSizeMm = TimestampIconSizeMm;

    public const double GeometryToleranceDip = 0.5;

    [Obsolete("rc15: outer frame removed.")]
    public const double MinCardToOuterFrameInsetMm = 0;

    public const double ContentCardWidthMm = ContentWidthMm;
    public static double BodyLeftSectionWidthMm => DipToMm(BodyLeftSectionWidthDip);
    public static double BodyRightSectionWidthMm => DipToMm(BodyRightSectionWidthDip);
    public static double IdentityCustomerCardWidthMm => DipToMm(IdentityCustomerCardWidthDip);
    public static double IdentityVehicleCardWidthMm => DipToMm(IdentityVehicleCardWidthDip);

    public static double PtToDip(double pt) => pt * Dpi / PointsPerInch;
    public static double MmToDip(double mm) => mm * Dpi / MmPerInch;

    public static double DipToMm(double dip) => dip * MmPerInch / Dpi;

    public static double TicketLogicalWidthDip => MmToDip(TicketLogicalWidthMm);
    public static double TicketLogicalHeightDip => MmToDip(TicketLogicalHeightMm);
    public static double A5LandscapePageWidthDip => MmToDip(A5LandscapePageWidthMm);
    public static double A5LandscapePageHeightDip => MmToDip(A5LandscapePageHeightMm);

    public static double SafeContentLeftDip => MmToDip(SafeContentLeftMm);
    public static double SafeContentRightDip => MmToDip(SafeContentRightMm);
    public static double SafeContentTopDip => MmToDip(SafeContentTopMm);
    public static double SafeContentBottomDip => MmToDip(SafeContentBottomMm);

    public static double HeaderLeftInsetLeftDip => MmToDip(HeaderLeftInsetLeftMm);
    public static double HeaderLeftInsetRightDip => MmToDip(HeaderLeftInsetRightMm);
    public static double HeaderLeftInsetTopDip => MmToDip(HeaderLeftInsetTopMm);
    public static double HeaderLeftInsetBottomDip => MmToDip(HeaderLeftInsetBottomMm);
    public static double HeaderRightMinInsetFromSafeRightDip => MmToDip(HeaderRightMinInsetFromSafeRightMm);
    public static double MinPlacementEdgeClearanceHorizontalDip => MmToDip(MinPlacementEdgeClearanceHorizontalMm);
    public static double MinPlacementEdgeClearanceVerticalDip => MmToDip(MinPlacementEdgeClearanceVerticalMm);

    public static double PageWidthDip => MmToDip(PageWidthMm);
    public static double PageHeightDip => MmToDip(PageHeightMm);
    public static double CopyHeightDip => MmToDip(CopyHeightMm);
    public static double CopyWidthDip => TicketLogicalWidthDip;

    public static double CopyPaddingLeftDip => MmToDip(CopyPaddingLeftMm);
    public static double CopyPaddingRightDip => MmToDip(CopyPaddingRightMm);
    public static double CopyPaddingTopDip => MmToDip(CopyPaddingTopMm);
    public static double CopyPaddingBottomDip => MmToDip(CopyPaddingBottomMm);

    public static double CopySafeRightEdgeDip => MmToDip(CopySafeRightEdgeMm);
    public static double CopySafeLeftEdgeDip => MmToDip(CopySafeLeftEdgeMm);
    public static double CopySafeTopEdgeDip => MmToDip(CopySafeTopEdgeMm);
    public static double CopySafeBottomEdgeDip => MmToDip(CopySafeBottomEdgeMm);

    public static double OuterFramePaddingDip => 0;
    public static double InnerLayoutWidthDip => ContentUsableWidthDip;
    public static double InnerLayoutHeightDip => ContentUsableHeightDip;
    public static double OuterFrameCornerRadiusDip => 0;
    public static double ContentCardWidthDip => ContentUsableWidthDip;
    [Obsolete("rc15: use ContentCardWidthDip.")]
    public static double InnerCardContentWidthDip => ContentCardWidthDip;
    [Obsolete("rc15: outer frame removed.")]
    public static double MinCardToOuterFrameInsetDip => 0;

    private static double FloorDip(double dip) => Math.Floor(dip);

    public static double WeightHeroCardWidthDip => FloorDip((ContentCardWidthDip - WeightCardGapDip * 2) / 3.0);
    public static double WeightHeroCardLastWidthDip => ContentCardWidthDip - WeightCardGapDip * 2 - WeightHeroCardWidthDip * 2;
    public static double BodyLeftSectionWidthDip => FloorDip(ContentCardWidthDip * BodyLeftColumnStar / (BodyLeftColumnStar + BodyRightColumnStar));
    public static double BodyRightSectionWidthDip => ContentCardWidthDip - BodyLeftSectionWidthDip - BodyLeftTimestampGapDip;
    public static double IdentityCustomerCardWidthDip => FloorDip(BodyLeftSectionWidthDip * IdentityCustomerColumnStar / (IdentityCustomerColumnStar + IdentityVehicleColumnStar));
    public static double IdentityVehicleCardWidthDip => BodyLeftSectionWidthDip - IdentityCustomerCardWidthDip - IdentityCardGapDip;

    public static double ContentUsableWidthDip => MmToDip(ContentWidthMm);
    public static double ContentUsableHeightDip => MmToDip(ContentHeightMm);

    public static double HeaderSectionHeightDip => MmToDip(HeaderSectionHeightMm);
    public static double TitleSectionHeightDip => MmToDip(TitleSectionHeightMm);
    public static double WeightHeroSectionHeightDip => MmToDip(WeightHeroSectionHeightMm);
    public static double BodySectionHeightDip => MmToDip(BodySectionHeightMm);
    public static double SignatureSectionHeightDip => MmToDip(SignatureSectionHeightMm);
    public static double SignatureBlankHeightDip => MmToDip(SignatureBlankHeightMm);

    public static double IdentityCardsHeightDip => MmToDip(IdentityCardsHeightMm);
    public static double IdentityDetailsGapDip => MmToDip(IdentityDetailsGapMm);
    public static double DetailsTableHeightDip => MmToDip(DetailsTableHeightMm);
    public static double HeroBodyGapDip => MmToDip(HeroBodyGapMm);
    public static double BodySignatureGapDip => MmToDip(BodySignatureGapMm);
    public static double CardHostInsetDip => MmToDip(CardHostInsetMm);
    public static double WeighBlock1HeightDip => MmToDip(WeighBlock1HeightMm);
    public static double WeighBlock2HeightDip => MmToDip(WeighBlock2HeightMm);
    public static double TimestampCardHeightDip => MmToDip(TimestampCardHeightMm);
    public static double SignDateRowHeightDip => MmToDip(SignDateRowHeightMm);

    public static double BodyLeftTimestampGapDip => MmToDip(BodyLeftTimestampGapMm);
    public static double WeightCardGapDip => MmToDip(WeightCardGapMm);
    public static double IdentityCardGapDip => MmToDip(IdentityCardGapMm);
    public static double IdentityIconTileWidthDip => MmToDip(IdentityIconTileWidthMm);
    public static double IdentityIconGraphicSizeDip => MmToDip(IdentityIconGraphicSizeMm);

    public static double DetailsTablePaddingLeftDip => MmToDip(DetailsTablePaddingLeftMm);
    public static double DetailsTablePaddingRightDip => MmToDip(DetailsTablePaddingRightMm);
    public static double DetailsTablePaddingTopDip => MmToDip(DetailsTablePaddingTopMm);
    public static double DetailsTablePaddingBottomDip => MmToDip(DetailsTablePaddingBottomMm);
    public static double DetailLabelInsetDip => MmToDip(DetailLabelInsetMm);
    public static double DetailValueInsetDip => MmToDip(DetailValueInsetMm);
    public static double DetailRowSeparatorInsetDip => MmToDip(DetailRowSeparatorInsetMm);
    public static double TimestampCardPaddingDip => MmToDip(TimestampCardPaddingHorizontalMm);
    public static double TimestampCardPaddingVerticalDip => MmToDip(TimestampCardPaddingVerticalMm);
    public static double TimestampIconColumnDip => MmToDip(TimestampIconColumnMm);
    public static double TimestampIconSizeDip => MmToDip(TimestampIconSizeMm);
    public static double CardCornerRadiusDip => MmToDip(CardCornerRadiusMm);
    public static double IdentityIconTileCornerRadiusDip =>
        Math.Max(0, CardCornerRadiusDip - IdentityIconTileBorderInsetDip);
    public static double WeightCardPaddingHorizontalDip => MmToDip(WeightCardPaddingHorizontalMm);
    public static double WeightCardPaddingVerticalDip => MmToDip(WeightCardPaddingVerticalMm);
    [Obsolete("rc15: use WeightCardPaddingHorizontalDip.")]
    public static double WeightCardPaddingDip => WeightCardPaddingHorizontalDip;
    public static double TimestampIconMaxSizeDip => MmToDip(TimestampIconMaxSizeMm);
    public static double PrinterImageableSafetyInsetHorizontalDip => MmToDip(PrinterImageableSafetyInsetHorizontalMm);
    public static double PrinterImageableSafetyInsetVerticalDip => MmToDip(PrinterImageableSafetyInsetVerticalMm);

    public static double HeaderRightPaddingLeftDip => MmToDip(HeaderRightPaddingLeftMm);
    public static double HeaderRightPaddingRightDip => MmToDip(HeaderRightPaddingRightMm);

    public static double TopCopyTopDip => 0;
    public static double BottomCopyTopDip => CopyHeightDip;

    public static double LayoutSectionsTotalHeightMm =>
        HeaderSectionHeightMm
        + TitleSectionHeightMm
        + WeightHeroSectionHeightMm
        + HeroBodyGapMm
        + BodySectionHeightMm
        + BodySignatureGapMm
        + SignatureSectionHeightMm;

    public static double ComputeImageableScale(
        double extentWidthDip,
        double extentHeightDip,
        double pageWidthDip,
        double pageHeightDip) =>
        Math.Min(extentWidthDip / pageWidthDip, extentHeightDip / pageHeightDip);

    [Obsolete("rc13: use CopyPaddingLeftMm.")]
    public const double PrintSafeHorizontalMarginLeftMm = CopyPaddingLeftMm;
    [Obsolete("rc13: use CopyPaddingRightMm.")]
    public const double PrintSafeHorizontalMarginRightMm = CopyPaddingRightMm;
    [Obsolete("rc10: use IdentityIconTileWidthMm.")]
    public const double IdentityIconColumnMm = IdentityIconTileWidthMm;
    [Obsolete("rc10: use IdentityIconTileWidthDip.")]
    public static double IdentityIconColumnDip => IdentityIconTileWidthDip;
    [Obsolete("rc10: use TimestampCardPaddingMm.")]
    public const double BodyRightPanelPaddingMm = TimestampCardPaddingMm;
    [Obsolete("rc10: use TimestampCardPaddingDip.")]
    public static double BodyRightPanelPaddingDip => TimestampCardPaddingDip;
    [Obsolete("rc13: use IdentityCustomerColumnStar.")]
    public const double IdentityCardColumnStar = IdentityCustomerColumnStar;
    [Obsolete("rc13: use BodySignatureGapMm.")]
    public const double SignatureRightPaddingMm = 0;
    [Obsolete("rc13: use BodySignatureGapDip.")]
    public static double SignatureRightPaddingDip => 0;
    [Obsolete("rc13: use IdentityValuePadding*Mm.")]
    public const double IdentityValueAreaPaddingMm = 2.0;
    [Obsolete("rc13: use MmToDip(IdentityValueAreaPaddingMm).")]
    public static double IdentityValueAreaPaddingDip => MmToDip(IdentityValueAreaPaddingMm);

    [Obsolete("rc9: use BodySectionHeightMm.")]
    public const double DetailsSectionHeightMm = BodySectionHeightMm;
    [Obsolete("rc9: use BodySectionHeightDip.")]
    public static double DetailsSectionHeightDip => BodySectionHeightDip;
    [Obsolete("rc7: no outer copy border.")]
    public const double CopyBorderThicknessDip = 0;
    [Obsolete("rc6: no cut band.")]
    public const double CutBandHeightMm = 0;
    [Obsolete("rc14: use OuterFrameBorderThicknessDip.")]
    public const double WeightCardBorderThicknessDip = CardBorderThicknessDip;
    [Obsolete("rc14: use CardBorderThicknessDip.")]
    public const double NetWeightCardBorderThicknessDip = CardBorderThicknessDip;
    [Obsolete("rc14: use WeightCardGapMm.")]
    public const double WeightCardGapMmLegacy = WeightCardGapMm;
    [Obsolete("rc14: use CardCornerRadiusMm.")]
    public const double WeightCardCornerRadiusMm = CardCornerRadiusMm;
    [Obsolete("rc8: use WeightCardPaddingHorizontalMm.")]
    public const double WeightCardPaddingMmLegacy = WeightCardPaddingHorizontalMm;
    [Obsolete("rc8: use BodyRightPanelPaddingMm.")]
    public const double BodyRightPaddingMm = BodyRightPanelPaddingMm;
    [Obsolete("rc8: use BodyRightPanelPaddingMm.")]
    public const double BodyRightMinPaddingMm = BodyRightPanelPaddingMm;
    [Obsolete("rc8: use FieldLabelColumnStar.")]
    public const double FieldLabelColumnStarLegacy = FieldLabelColumnStar;
    [Obsolete("rc8: use FieldValueColumnStar.")]
    public const double FieldValueColumnStarLegacy = FieldValueColumnStar;
    [Obsolete("rc8: icons optional in timestamp panel.")]
    public const double WeighIconColumnMm = TimestampIconColumnMm;
    [Obsolete("rc8: icons optional in timestamp panel.")]
    public static double WeighIconColumnDip => TimestampIconColumnDip;
    [Obsolete("rc8: icons optional in timestamp panel.")]
    public static double WeighIconMaxSizeDip => TimestampIconMaxSizeDip;
    [Obsolete("rc8: use BodySectionHeightDip.")]
    public static double MainSectionHeightDip => BodySectionHeightDip;
    [Obsolete("rc8: use BodyLeftColumnStar.")]
    public const double MainLeftColumnStar = BodyLeftColumnStar;
    [Obsolete("rc8: use BodyRightColumnStar.")]
    public const double MainRightColumnStar = BodyRightColumnStar;
    [Obsolete("rc6: use CopyHeightDip.")]
    public static double UsableCopyHeightDip => CopyHeightDip;
    [Obsolete("rc6: use CopyWidthDip.")]
    public static double ContentWidthDip => CopyWidthDip;
    [Obsolete("rc6: no cut band.")]
    public static double CutLineCenterDip => CopyHeightDip;
    [Obsolete("rc6: no page outer margin.")]
    public static double OuterMarginLeftDip => 0;
    [Obsolete("rc6: no page outer margin.")]
    public static double OuterMarginTopDip => 0;
    [Obsolete("rc8: removed.")]
    public static double SignDateBottomMarginDip => 0;
}

public static class WeighTicketPrintTypography
{
    public const double StationNamePt = 14.0;
    public const double AddressPt = 10.0;
    public const double StationTypePt = 12.5;
    public const double TicketLabelPt = 11.5;
    public const double TicketNumberPt = 18.0;
    public const double TitlePt = 24.0;

    public const double HeroCardLabelPt = 11.5;
    public const double HeroWeightValuePt = 30.0;
    public const double HeroWeightUnitPt = 11.5;

    public const double CustomerValuePt = 16.5;
    public const double CustomerValueMinPt = 12.5;
    public const double PlateValuePt = 18.0;
    public const double PlateValueMinPt = 14.5;

    public const double DetailLabelPt = 10.5;
    public const double DetailValuePt = 11.5;
    public const double DetailAmountPt = 12.5;

    public const double WeighTitlePt = 10.0;
    public const double WeighTimePt = 15.5;
    public const double WeighTimeMinPt = 14.5;
    public const double WeighDatePt = 10.5;
    public const double SignDatePt = 9.0;

    public const double SignatureRolePt = 12.0;
    public const double SignatureHintPt = 10.0;

    public static double StationNameDip => WeighTicketPrintLayout.PtToDip(StationNamePt);
    public static double AddressDip => WeighTicketPrintLayout.PtToDip(AddressPt);
    public static double StationTypeDip => WeighTicketPrintLayout.PtToDip(StationTypePt);
    public static double TicketLabelDip => WeighTicketPrintLayout.PtToDip(TicketLabelPt);
    public static double TicketNumberDip => WeighTicketPrintLayout.PtToDip(TicketNumberPt);
    public static double TitleDip => WeighTicketPrintLayout.PtToDip(TitlePt);

    public static double HeroCardLabelDip => WeighTicketPrintLayout.PtToDip(HeroCardLabelPt);
    public static double HeroWeightValueDip => WeighTicketPrintLayout.PtToDip(HeroWeightValuePt);
    public static double HeroWeightUnitDip => WeighTicketPrintLayout.PtToDip(HeroWeightUnitPt);

    public static double CustomerValueDip => WeighTicketPrintLayout.PtToDip(CustomerValuePt);
    public static double CustomerValueMinDip => WeighTicketPrintLayout.PtToDip(CustomerValueMinPt);
    public static double PlateValueDip => WeighTicketPrintLayout.PtToDip(PlateValuePt);
    public static double PlateValueMinDip => WeighTicketPrintLayout.PtToDip(PlateValueMinPt);

    public static double WeighTimeMinDip => WeighTicketPrintLayout.PtToDip(WeighTimeMinPt);

    public static double DetailLabelDip => WeighTicketPrintLayout.PtToDip(DetailLabelPt);
    public static double DetailValueDip => WeighTicketPrintLayout.PtToDip(DetailValuePt);
    public static double DetailAmountDip => WeighTicketPrintLayout.PtToDip(DetailAmountPt);

    public static double WeighTitleDip => WeighTicketPrintLayout.PtToDip(WeighTitlePt);
    public static double WeighTimeDip => WeighTicketPrintLayout.PtToDip(WeighTimePt);
    public static double WeighDateDip => WeighTicketPrintLayout.PtToDip(WeighDatePt);
    public static double SignDateDip => WeighTicketPrintLayout.PtToDip(SignDatePt);

    public static double SignatureRoleDip => WeighTicketPrintLayout.PtToDip(SignatureRolePt);
    public static double SignatureHintDip => WeighTicketPrintLayout.PtToDip(SignatureHintPt);

    [Obsolete("rc9: use HeroWeightValuePt.")]
    public const double HeroNetWeightValuePt = HeroWeightValuePt;
    [Obsolete("rc9: use HeroWeightValueDip.")]
    public static double HeroNetWeightValueDip => HeroWeightValueDip;
    [Obsolete("rc8: use DetailLabelPt.")]
    public const double MainLabelPt = DetailLabelPt;
    [Obsolete("rc8: use DetailValuePt.")]
    public const double MainValuePt = DetailValuePt;
    [Obsolete("rc8: use DetailAmountPt.")]
    public const double MainAmountPt = DetailAmountPt;
    [Obsolete("rc8: use DetailLabelDip.")]
    public static double MainLabelDip => DetailLabelDip;
    [Obsolete("rc8: use DetailValueDip.")]
    public static double MainValueDip => DetailValueDip;
    [Obsolete("rc8: use DetailAmountDip.")]
    public static double MainAmountDip => DetailAmountDip;
    [Obsolete("rc8: use HeroWeightValueDip.")]
    public static double MainWeightDip => HeroWeightValueDip;
}
