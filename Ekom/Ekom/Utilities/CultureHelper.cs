using System.Globalization;

namespace Ekom.Utilities
{
    static class CultureHelper
    {
        public static CultureInfo GetCultureInfoByCurrencyCode(string currencyCode)
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
