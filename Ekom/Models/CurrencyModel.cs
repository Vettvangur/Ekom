using System.Globalization;

namespace Ekom.Models
{
    public class CurrencyModel
    {
        public string CurrencyFormat { get; set; }
        public string CurrencyValue { get; set; }
        public string CurrencySymbol
        {
            get
            {
                return !string.IsNullOrEmpty(CurrencyValue) ? new RegionInfo(CurrencyValue).CurrencySymbol : string.Empty;
            }
        }
        public string ISOCurrencySymbol
        {
            get
            {
                return !string.IsNullOrEmpty(CurrencyValue) ? new RegionInfo(CurrencyValue).ISOCurrencySymbol : string.Empty;
            }
        }
    }
}
