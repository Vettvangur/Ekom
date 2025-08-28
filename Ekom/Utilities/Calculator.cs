namespace Ekom.Utilities;

public static class Calculator
{
    /// <summary>
    /// Removes VAT from amount.
    /// </summary>
    /// <param name="withVatVal">The with vat.</param>
    /// <param name="vatVal">The vat.</param>
    /// <returns></returns>
    public static decimal WithoutVat(decimal withVatVal, decimal vatVal, string currency)
    {
        if (vatVal == 0m)
        {
            // Still apply ISK rounding policy to the input amount.
            return PerformVatRounding(withVatVal, currency);
        }
        return PerformVatRounding(withVatVal / (1 + vatVal), currency);
    }

    /// <summary>
    /// Returns the amount with VAT included.
    /// </summary>
    /// <returns></returns>
    public static decimal WithVat(decimal withoutVatVal, decimal vatVal, string currency)
    {
        if (vatVal == 0m)
        {
            // Still apply ISK rounding policy to the input amount.
            return PerformVatRounding(withoutVatVal, currency);
        }
        return PerformVatRounding(withoutVatVal * (1 + vatVal), currency);
    }

    /// <summary>
    /// Calculates the VAT amount that would be added if VAT would be applied.
    /// </summary>
    /// <returns></returns>
    public static decimal VatAmountFromWithoutVat(decimal withoutVatVal, decimal vatVal, string currency)
    {
        if (vatVal == 0m) return 0m;
        return WithVat(withoutVatVal, vatVal, currency) - withoutVatVal;
    }

    /// <summary>
    /// Calculates the VAT amount included in a price including VAT already.
    /// </summary>
    /// <returns></returns>
    public static decimal VatAmountFromWithVat(decimal withoutVatVal, decimal vatVal, string currency)
    {
        if (vatVal == 0m) return 0m;
        return withoutVatVal - WithoutVat(withoutVatVal, vatVal, currency);
    }

    private static decimal PerformVatRounding(decimal val, string currency)
    {
        if (currency.ToUpperInvariant() == "ISK")
        {
            return EkomRounding(val, Configuration.Instance.VatCalculationRounding);
        }

        return val;
    }

    public static decimal EkomRounding(decimal val, Rounding rounding, int decimals = 0)
    {
        // Normalize to given decimal places (default 0) before rounding
        val = Math.Round(val, decimals + 5, MidpointRounding.AwayFromZero);

        switch (rounding)
        {
            case Rounding.None:
                return val;

            case Rounding.RoundDown:
                return Math.Floor(val * (decimal)Math.Pow(10, decimals)) / (decimal)Math.Pow(10, decimals);

            case Rounding.RoundUp:
                return Math.Ceiling(val * (decimal)Math.Pow(10, decimals)) / (decimal)Math.Pow(10, decimals);

            case Rounding.RoundToEven:
                return Math.Round(val, decimals, MidpointRounding.ToEven);

            case Rounding.AwayFromZero:
                return Math.Round(val, decimals, MidpointRounding.AwayFromZero);

            default:
                throw new ArgumentOutOfRangeException(nameof(rounding), rounding, "Unknown rounding specified");
        }
    }

    public static (decimal UnitNet, decimal UnitVat) SplitVatFromGrossPerUnit(
        decimal unitGross, decimal vatRate, string currency)
    {
        if (vatRate == 0m)
        {
            // Round per ISK policy; VAT is zero; net == gross after rounding.
            var rounded = PerformVatRounding(unitGross, currency);
            return (rounded, 0m);
        }

        var unitNet = WithoutVat(unitGross, vatRate, currency);
        var unitVat = unitGross - unitNet;
        return (unitNet, unitVat);
    }

    public static decimal VatFromNet(decimal withoutVatVal, decimal vatVal, string currency)
    {
        var vat = withoutVatVal * vatVal; // vatVal = 0.24m for 24%
        return PerformVatRounding(vat, currency); // ISK → round to 0 with configured mode
    }
}
