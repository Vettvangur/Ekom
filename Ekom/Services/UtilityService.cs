using System;

namespace Ekom.Services
{
    internal class UtilityService
    {
        public static DateTime ConvertToDatetime(string value)
        {
            try
            {
                return DateTime.ParseExact(value, "yyyy-MM-dd HH:mm:ss:fff", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return new DateTime(Convert.ToInt64(value));
            }
        }
    }
}
