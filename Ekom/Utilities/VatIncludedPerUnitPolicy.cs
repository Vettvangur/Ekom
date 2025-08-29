namespace Ekom.Utilities;

public enum VatIncludedPerUnitPolicy
{
    LineLevelVat = 0,        // recompute VAT from the line net. By using LineLevelVat the unit Vat and total vat of the order will not be in sync
    PreserveStickerGross = 1 // per-unit residuals; gross = sticker × qty
}
