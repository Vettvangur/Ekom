using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Interfaces;

namespace uWebshop.Models
{
    public class Price : IPrice
    {
        private decimal originalPrice;

        public Price(decimal originalPrice)
        {
            this.originalPrice = originalPrice;
        }

        public decimal Value {
            get
            {
                return originalPrice;
            }
            set { }
        }


        public IPrice WithVat
        {
            get
            {
                return this;
            }
        }
        public IPrice WithoutVat
        {
            get
            {
                return this;
            }
        }

        public IVatPrice BeforeDiscount
        {
            get
            {
                return null;
            }
        }

        public string ToCurrencyString()
        {
            return Value.ToString("C");
            //return (ValueInCents / 100m).ToString("C", StoreHelper.GetCurrencyCulture(_localization));
        }
    }
}
