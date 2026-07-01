using System.Globalization;

namespace Ekom.Utilities
{
    static class CultureHelper
    {
        public static CultureInfo? GetCultureInfo(string? currency)
        {
            if (string.IsNullOrWhiteSpace(currency))
            {
                return null;
            }

            if (currency.Length == 3 && !currency.Contains('-', StringComparison.Ordinal))
            {
                return GetCultureInfoByCurrencyCode(currency);
            }

            try
            {
                var cultureInfo = new CultureInfo(currency);

                return cultureInfo.TwoLetterISOLanguageName == "is" ? Configuration.IsCultureInfo : cultureInfo;
            }
            catch (CultureNotFoundException)
            {
                return GetCultureInfoByCurrencyCode(currency);
            }
        }

        public static CultureInfo? GetCultureInfoByCurrencyCode(string currencyCode)
        {
            return CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .FirstOrDefault(c =>
                {
                    try
                    {
                        RegionInfo region = new RegionInfo(c.Name);
                        return region.ISOCurrencySymbol == currencyCode;
                    }
                    catch (ArgumentException)
                    {
                        // Ignore cultures that do not have region information
                        return false;
                    }
                });
        }
    }
}
