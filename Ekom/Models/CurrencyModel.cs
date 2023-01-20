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
                return new RegionInfo(CurrencyValue).CurrencySymbol;
            }
        }
        public string ISOCurrencySymbol
        {
            get
            {
                return new RegionInfo(CurrencyValue).ISOCurrencySymbol;
            }
        }
    }
}
