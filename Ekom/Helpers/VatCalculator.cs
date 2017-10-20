using System;

namespace Ekom.Helpers
{
    static class VatCalculator
    {
        /// <summary>
        /// Without vat.
        /// </summary>
        /// <param name="withVat">The with vat.</param>
        /// <param name="vat">The vat.</param>
        /// <returns></returns>
        public static int WithoutVat(int withVat, decimal vat)
        {
            return withVat - VatAmountFromWithVat(withVat, vat);
            //return (int)Math.Ceiling(withVat / (100 + vat) * 100); // correct(?)
        }

        /// <summary>
        /// With vat.
        /// </summary>
        /// <param name="withoutVat">The without vat.</param>
        /// <param name="vat">The vat.</param>
        /// <returns></returns>
        public static int WithVat(int withoutVat, decimal vat)
        {
            return (int)Math.Round(withoutVat * (100 + vat) / 100, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Vats the amount from without vat.
        /// </summary>
        /// <param name="withoutVat">The without vat.</param>
        /// <param name="vat">The vat.</param>
        /// <returns></returns>
        public static int VatAmountFromWithoutVat(int withoutVat, decimal vat)
        {
            return WithVat(withoutVat, vat) - withoutVat;
        }

        /// <summary>
        /// Vats the amount from with vat.
        /// </summary>
        /// <param name="withVat">The with vat.</param>
        /// <param name="vat">The vat.</param>
        /// <returns></returns>
        public static int VatAmountFromWithVat(int withVat, decimal vat)
        {
            return (int)Math.Round(withVat - (withVat / (100m + vat) * 100m), MidpointRounding.AwayFromZero); // verified correct
        }

        /// <summary>
        /// Gets the vat amount from the original amount.
        /// </summary>
        /// <param name="original">The original.</param>
        /// <param name="vat">The vat.</param>
        /// <returns></returns>
        public static int VatAmountFromOriginal(int original, decimal vat)
        {
            return vat > 0 ? VatAmountFromWithVat(original, vat) : VatAmountFromWithoutVat(original, vat);
        }
    }
}
