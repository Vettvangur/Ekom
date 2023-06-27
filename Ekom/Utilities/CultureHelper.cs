using System.Globalization;

namespace Ekom.Utilities
{
    static class CultureHelper
    {
        public static CultureInfo GetCultureInfoByCurrencyCode(string currencyCode)
        {
            return CultureInfo.GetCultures(CultureTypes.AllCultures)
                .Where(c => !c.IsNeutralCulture)
                .FirstOrDefault(c =>
                {
                    try
                    {
                        return new RegionInfo(c.LCID).ISOCurrencySymbol == currencyCode;
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
