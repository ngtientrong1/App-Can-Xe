using System.Windows;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Views.Printing;

internal static class TicketPrintLayoutXaml
{
    public static GridLength HeaderSectionRow => new(WeighTicketPrintLayout.HeaderSectionHeightDip);
    public static GridLength TitleSectionRow => new(WeighTicketPrintLayout.TitleSectionHeightDip);
    public static GridLength WeightHeroSectionRow => new(WeighTicketPrintLayout.WeightHeroSectionHeightDip);
    public static GridLength BodySectionRow => new(WeighTicketPrintLayout.BodySectionHeightDip);
    public static GridLength SignatureSectionRow => new(WeighTicketPrintLayout.SignatureSectionHeightDip);
    public static GridLength SignatureBlankRow => new(WeighTicketPrintLayout.SignatureBlankHeightDip);

    public static GridLength IdentityCardsRow => new(WeighTicketPrintLayout.IdentityCardsHeightDip);
    public static GridLength IdentityDetailsSpacerRow => new(WeighTicketPrintLayout.IdentityDetailsGapDip);
    public static GridLength DetailsTableRow => new(WeighTicketPrintLayout.DetailsTableHeightDip);

    public static GridLength BodyWeighBandRow => new(WeighTicketPrintLayout.BodyWeighBandHeightDip);
    public static GridLength BodyRowGapRow => new(WeighTicketPrintLayout.BodyRowGapDip);
    public static GridLength BodyInfoBandRow => new(WeighTicketPrintLayout.BodyInfoBandHeightDip);
    public static GridLength BodyBottomRowRow => new(WeighTicketPrintLayout.BodyBottomRowHeightDip);
    public static GridLength BandDividerColumn => new(WeighTicketPrintLayout.ColumnSeparatorThicknessDip);
    public static GridLength HeroBodySpacerRow => new(WeighTicketPrintLayout.HeroBodyGapDip);
    public static GridLength BodySignatureSpacerRow => new(WeighTicketPrintLayout.BodySignatureGapDip);
    public static GridLength WeighBlock1Row => new(WeighTicketPrintLayout.WeighBlock1HeightDip);
    public static GridLength WeighBlock2Row => new(WeighTicketPrintLayout.WeighBlock2HeightDip);
    public static GridLength TimestampCardRow => new(WeighTicketPrintLayout.TimestampCardHeightDip);
    public static GridLength SignDateRow => new(WeighTicketPrintLayout.SignDateRowHeightDip);

    public static GridLength DetailFieldRow => new(1, GridUnitType.Star);
    public static GridLength SeparatorRow => new(WeighTicketPrintLayout.RowSeparatorThicknessDip);
    public static GridLength DashedSeparatorRow => new(WeighTicketPrintLayout.DashedSeparatorThicknessDip);
    public static GridLength DetailRowSeparatorRow => new(WeighTicketPrintLayout.DetailRowSeparatorThicknessDip);

    public static GridLength WeightCardGapColumn => new(WeighTicketPrintLayout.WeightCardGapDip);
    public static GridLength IdentityCardGapColumn => new(WeighTicketPrintLayout.IdentityCardGapDip);
    public static GridLength IdentityIconTileColumn => new(WeighTicketPrintLayout.IdentityIconTileWidthDip);
    public static GridLength BodyGapColumn => new(WeighTicketPrintLayout.BodyLeftTimestampGapDip);
    public static GridLength TimestampIconColumn => new(WeighTicketPrintLayout.TimestampIconColumnDip);
    public static GridLength IdentityIconSeparatorColumn => new(WeighTicketPrintLayout.IdentityIconSeparatorThicknessDip);

    public static GridLength HeaderSeparatorColumn => new(WeighTicketPrintLayout.HeaderColumnSeparatorThicknessDip);
    public static GridLength SignatureSeparatorColumn => new(WeighTicketPrintLayout.SignatureColumnSeparatorDip);

    public static GridLength HeaderLeftColumn => new(WeighTicketPrintLayout.HeaderLeftColumnStar, GridUnitType.Star);
    public static GridLength HeaderRightColumn => new(WeighTicketPrintLayout.HeaderRightColumnStar, GridUnitType.Star);
    public static GridLength BodyLeftColumn => new(WeighTicketPrintLayout.BodyLeftColumnStar, GridUnitType.Star);
    public static GridLength BodyRightColumn => new(WeighTicketPrintLayout.BodyRightColumnStar, GridUnitType.Star);
    public static GridLength FieldLabelColumn => new(WeighTicketPrintLayout.FieldLabelColumnStar, GridUnitType.Star);
    public static GridLength FieldValueColumn => new(WeighTicketPrintLayout.FieldValueColumnStar, GridUnitType.Star);
    public static GridLength ContentCardColumn => new(WeighTicketPrintLayout.ContentCardWidthDip);

    public static GridLength WeightHeroCardLastColumn => new(WeighTicketPrintLayout.WeightHeroCardLastWidthDip);
    public static GridLength WeightHeroCardColumn => new(WeighTicketPrintLayout.WeightHeroCardWidthDip);
    public static GridLength BodyLeftSectionColumn => new(WeighTicketPrintLayout.BodyLeftSectionWidthDip);
    public static GridLength BodyRightSectionColumn => new(WeighTicketPrintLayout.BodyRightSectionWidthDip);
    public static GridLength IdentityCustomerCardColumn => new(WeighTicketPrintLayout.IdentityCustomerCardWidthDip);
    public static GridLength IdentityVehicleCardColumn => new(WeighTicketPrintLayout.IdentityVehicleCardWidthDip);

    public static GridLength WeightHeroColumn => new(WeighTicketPrintLayout.WeightHeroColumnStar, GridUnitType.Star);
    public static GridLength IdentityCustomerColumn => new(WeighTicketPrintLayout.IdentityCustomerColumnStar, GridUnitType.Star);
    public static GridLength IdentityVehicleColumn => new(WeighTicketPrintLayout.IdentityVehicleColumnStar, GridUnitType.Star);

    public static Thickness SafeContentPadding => new(
        WeighTicketPrintLayout.SafeContentLeftDip,
        WeighTicketPrintLayout.SafeContentTopDip,
        WeighTicketPrintLayout.SafeContentRightDip,
        WeighTicketPrintLayout.SafeContentBottomDip);

    public static Thickness HeaderLeftInset => new(
        WeighTicketPrintLayout.HeaderLeftInsetLeftDip,
        WeighTicketPrintLayout.HeaderLeftInsetTopDip,
        WeighTicketPrintLayout.HeaderLeftInsetRightDip,
        WeighTicketPrintLayout.HeaderLeftInsetBottomDip);

    public static Thickness HeaderRightInset => new(
        WeighTicketPrintLayout.HeaderRightPaddingLeftDip,
        0,
        WeighTicketPrintLayout.HeaderRightPaddingRightDip,
        0);

    public static Thickness TimestampCardPadding => new(
        WeighTicketPrintLayout.TimestampCardPaddingDip,
        WeighTicketPrintLayout.TimestampCardPaddingVerticalDip,
        WeighTicketPrintLayout.TimestampCardPaddingDip,
        WeighTicketPrintLayout.TimestampCardPaddingVerticalDip);

    public static Thickness IdentityValuePadding => new(
        WeighTicketPrintLayout.MmToDip(WeighTicketPrintLayout.IdentityValuePaddingLeftMm),
        WeighTicketPrintLayout.MmToDip(WeighTicketPrintLayout.IdentityValuePaddingTopMm),
        WeighTicketPrintLayout.MmToDip(WeighTicketPrintLayout.IdentityValuePaddingRightMm),
        WeighTicketPrintLayout.MmToDip(WeighTicketPrintLayout.IdentityValuePaddingBottomMm));

    public static Thickness DetailsTablePadding => new(
        WeighTicketPrintLayout.DetailsTablePaddingLeftDip,
        WeighTicketPrintLayout.DetailsTablePaddingTopDip,
        WeighTicketPrintLayout.DetailsTablePaddingRightDip,
        WeighTicketPrintLayout.DetailsTablePaddingBottomDip);

    public static Thickness DetailLabelInset => new(WeighTicketPrintLayout.DetailLabelInsetDip, 0, 0, 0);
    public static Thickness DetailValueInset => new(0, 0, WeighTicketPrintLayout.DetailValueInsetDip, 0);
    public static Thickness DetailRowSeparatorInset => new(WeighTicketPrintLayout.DetailRowSeparatorInsetDip, 0, WeighTicketPrintLayout.DetailRowSeparatorInsetDip, 0);

    public static Thickness WeightCardPadding => new(
        WeighTicketPrintLayout.WeightCardPaddingHorizontalDip,
        WeighTicketPrintLayout.WeightCardPaddingVerticalDip,
        WeighTicketPrintLayout.WeightCardPaddingHorizontalDip,
        WeighTicketPrintLayout.WeightCardPaddingVerticalDip);

    public static Thickness CardHostInset => new(WeighTicketPrintLayout.CardHostInsetDip);

    public static Thickness IdentityIconTileInset => new(WeighTicketPrintLayout.IdentityIconTileBorderInsetDip);

    public static Thickness CardBorderThickness => new(WeighTicketPrintLayout.CardBorderThicknessDip);
    public static CornerRadius CardCornerRadius => new(WeighTicketPrintLayout.CardCornerRadiusDip);

    public static CornerRadius IdentityIconTileCornerRadius => new(
        WeighTicketPrintLayout.IdentityIconTileCornerRadiusDip,
        0,
        0,
        WeighTicketPrintLayout.IdentityIconTileCornerRadiusDip);

    public static Thickness SignatureRoleInset => new(
        0,
        WeighTicketPrintLayout.MmToDip(1.5),
        0,
        0);

    public static double IdentityIconGraphicSize => WeighTicketPrintLayout.IdentityIconGraphicSizeDip;
    public static double TimestampIconSize => WeighTicketPrintLayout.TimestampIconSizeDip;
    public static double TimestampIconStrokeThickness => WeighTicketPrintLayout.TimestampIconStrokeThicknessDip;

    [Obsolete("rc15: outer frame removed.")]
    public static Thickness OuterFramePadding => new(0);
    [Obsolete("rc15: outer frame removed.")]
    public static Thickness OuterFrameBorderThickness => new(0);
    [Obsolete("rc15: outer frame removed.")]
    public static CornerRadius OuterFrameCornerRadius => new(0);
    [Obsolete("rc15: use ContentCardColumn.")]
    public static GridLength InnerCardContentColumn => ContentCardColumn;
    [Obsolete("rc15: outer frame removed.")]
    public static GridLength InnerCardHorizontalInsetColumn => new(0);
    [Obsolete("rc14: use BodySignatureSpacerRow inside signature section.")]
    public static Thickness SignatureSectionMargin => new(0, WeighTicketPrintLayout.BodySignatureGapDip, 0, 0);
    [Obsolete("rc14: use IdentityDetailsSpacerRow.")]
    public static Thickness IdentityDetailsGapMargin => new(0, WeighTicketPrintLayout.IdentityDetailsGapDip, 0, 0);
    [Obsolete("rc14: use IdentityCustomerColumn.")]
    public static GridLength IdentityCardColumn => IdentityCustomerColumn;
    [Obsolete("rc14: use BodyGapColumn.")]
    public static GridLength BodySeparatorColumn => BodyGapColumn;
    [Obsolete("rc14: use IdentityIconTileColumn.")]
    public static GridLength IdentityIconColumn => IdentityIconTileColumn;
    [Obsolete("rc14: use TimestampCardPadding.")]
    public static Thickness TimestampPanelPadding => TimestampCardPadding;
    [Obsolete("rc14: use BodySectionRow.")]
    public static GridLength DetailsSectionRow => BodySectionRow;
    [Obsolete("rc14: removed.")]
    public static Thickness SignatureRightInset => new(0);
    [Obsolete("rc14: removed.")]
    public static Thickness SignatureTopInset => new(0, WeighTicketPrintLayout.BodySignatureGapDip, 0, 0);
    [Obsolete("rc14: use IdentityValuePadding.")]
    public static Thickness IdentityValueAreaPadding => IdentityValuePadding;
}
