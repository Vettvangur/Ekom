using System;

namespace Ekom.Helpers
{
    static class VatCalculator
    {
        /// <summary>
        /// Removes VAT from amount.
        /// </summary>
        /// <param name="withVat">The with vat.</param>
        /// <param name="vat">The vat.</param>
        /// <returns></returns>
        public static decimal WithoutVat(decimal withVat, decimal vat)
        {
            return withVat / (1 + vat);
        }

        /// <summary>
        /// Returns the amount with VAT included.
        /// </summary>
        /// <param name="withoutVat">The without vat.</param>
        /// <param name="vat">The vat.</param>
        /// <returns></returns>
        public static decimal WithVat(decimal withoutVat, decimal vat)
        {
            return withoutVat * (1 + vat);
        }

        /// <summary>
        /// Calculates the VAT amount that would be added if VAT would be applied.
        /// </summary>
        /// <param name="withoutVat">The without vat.</param>
        /// <param name="vat">The vat.</param>
        /// <returns></returns>
        public static decimal VatAmountFromWithoutVat(decimal withoutVat, decimal vat)
        {
            return WithVat(withoutVat, vat) - withoutVat;
        }

        /// <summary>
        /// Calculates the VAT amount included in a price including VAT already.
        /// </summary>
        /// <param name="withVat">The with vat.</param>
        /// <param name="vat">The vat.</param>
        /// <returns></returns>
        public static decimal VatAmountFromWithVat(decimal withVat, decimal vat)
        {
            return withVat - WithoutVat(withVat, vat); // verified correct
        }

        private static decimal PerformRounding(decimal val)
        {
            switch (Configuration.Current.VatCalculationRounding)
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
