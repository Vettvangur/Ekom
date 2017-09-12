using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Interfaces;
using Microsoft.Practices.Unity;
using uWebshop.Services;
using uWebshop.App_Start;

namespace uWebshop.Models
{
    public class Price : IDiscountedPrice
    {
        private decimal originalPrice;
        private string culture;
        private decimal vat;
        private bool vatIncludeInPrice;
        private Configuration _config;

        private readonly IPriceCalculationService _priceCalculationService;

        public Price(decimal originalPrice, Store store)
        {
            this.originalPrice = originalPrice;
            this.vat = store.Vat;
            this.vatIncludeInPrice = store.VatIncludedInPrice;
            this.culture = store.Culture.Name;
            this._config = new Configuration();

            _priceCalculationService = new PriceCalculationService();
        }

        public Price(decimal originalPrice, StoreInfo storeInfo)
        {
            this.originalPrice = originalPrice;
            this.vat = storeInfo.Vat;
            this.vatIncludeInPrice = storeInfo.VatIncludedInPrice;
            this.culture = storeInfo.Culture;

            _priceCalculationService = new PriceCalculationService();
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
                return new SimplePrice(true,originalPrice,culture,vat,vatIncludeInPrice);
            }
        }
        public IPrice WithoutVat
        {
            get
            {
                return new SimplePrice(false,originalPrice, culture, vat, vatIncludeInPrice);
            }
        }

        public IVatPrice BeforeDiscount
        {
            get;
        }

        public IVatPrice Discount { get; }
        public IPrice Vat { get; }

        //public string ToCurrencyString
        //{
         
        //    get
        //    {
        //        return Value.ToString(_config.CurrencyFormat);
        //    }

        //    //return (ValueInCents / 100m).ToString("C", StoreHelper.GetCurrencyCulture(_localization));
        //}
    }

    public class SimplePrice : IPrice
    {
        private decimal originalPrice;
        private string culture;
        private decimal vat;
        private bool includeVat;
        private bool vatIncludeInPrice;
        private Configuration _config;

        public SimplePrice(bool includeVat, decimal originalPrice, string culture, decimal vat, bool vatIncludeInPrice)
        {
            this.originalPrice = originalPrice;
            this.culture = culture;
            this.vat = vat;
            this.includeVat = includeVat;
            this._config = new Configuration();
        }

        public decimal Value {
            get {
                return GetAmount();
            }
        }
        public string ToCurrencyString
        {
            get
            {
                var amount = GetAmount();

                return (amount).ToString(_config.CurrencyFormat, new CultureInfo(culture));
            }

        }

        private decimal GetAmount()
        {
            var price = originalPrice;

            var vatAmount = price * (vat / 100m);

            if (vatIncludeInPrice)
            {
                if (!includeVat)
                {
                    price = price - vatAmount;
                }

            } else
            {
                if (includeVat)
                {
                    price = price + vatAmount;
                }
            }

            return price;
        }
    }
}
