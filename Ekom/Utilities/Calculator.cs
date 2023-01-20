using System;

namespace Ekom.Utilities
{
    static class Calculator
    {
        /// <summary>
        /// Removes VAT from amount.
        /// </summary>
        /// <param name="withVatVal">The with vat.</param>
        /// <param name="vatVal">The vat.</param>
        /// <returns></returns>
        public static decimal WithoutVat(decimal withVatVal, decimal vatVal, string currency)
        {
            return PerformVatRounding(withVatVal / (1 + vatVal), currency);
        }

        /// <summary>
        /// Returns the amount with VAT included.
        /// </summary>
        /// <returns></returns>
        public static decimal WithVat(decimal withoutVatVal, decimal vatVal, string currency)
        {
            return PerformVatRounding(withoutVatVal * (1 + vatVal), currency);
        }

        /// <summary>
        /// Calculates the VAT amount that would be added if VAT would be applied.
        /// </summary>
        /// <returns></returns>
        public static decimal VatAmountFromWithoutVat(decimal withoutVatVal, decimal vatVal, string currency)
        {
            return WithVat(withoutVatVal, vatVal, currency) - withoutVatVal;
        }

        /// <summary>
        /// Calculates the VAT amount included in a price including VAT already.
        /// </summary>
        /// <returns></returns>
        public static decimal VatAmountFromWithVat(decimal withoutVatVal, decimal vatVal, string currency)
        {
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

        public static decimal EkomRounding(decimal val, Rounding rounding)
        {
            switch (rounding)
            {
                case Rounding.None:
                    return val;

                case Rounding.RoundDown:
                    return Math.Floor(val);

                case Rounding.RoundUp:
                    return Math.Ceiling(val);

                case Rounding.RoundToEven:
                    return Math.Round(val, MidpointRounding.ToEven);

                case Rounding.AwayFromZero:
                    return Math.Round(val, MidpointRounding.AwayFromZero);

                default:
                    // impossible
                    throw new Exception("Unknown rounding specified");
            }
        }
    }
}
