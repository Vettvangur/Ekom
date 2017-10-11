using System.Globalization;
using System.Web.Mvc;
using uWebshop.Interfaces;
using uWebshop.Services;

namespace uWebshop.Models
{
    public class Price : IDiscountedPrice
    {
        private decimal _originalPrice;
        private string _culture;
        private decimal _vat;
        private bool _vatIncludeInPrice;

        private readonly IPriceCalculationService _priceCalculationService;

        public Price(string originalPrice, Store store)
        {
            if (decimal.TryParse(originalPrice, out decimal result))
            {
                _originalPrice = result;
            }
            else
            {
                _originalPrice = 0;
            }

            _vat = store.Vat;
            _vatIncludeInPrice = store.VatIncludedInPrice;
            _culture = store.Culture.Name;

            _priceCalculationService = new PriceCalculationService();
        }

        public Price(decimal originalPrice, Store store)
        {
            _originalPrice = originalPrice;
            _vat = store.Vat;
            _vatIncludeInPrice = store.VatIncludedInPrice;
            _culture = store.Culture.Name;

            _priceCalculationService = new PriceCalculationService();
        }

        public Price(decimal originalPrice, StoreInfo storeInfo)
        {
            _originalPrice = originalPrice;
            _vat = storeInfo.Vat;
            _vatIncludeInPrice = storeInfo.VatIncludedInPrice;
            _culture = storeInfo.Culture;

            _priceCalculationService = new PriceCalculationService();
        }


        public decimal Value
        {
            get
            {
                return _originalPrice;
            }
            set { }
        }


        public IPrice WithVat
        {
            get
            {
                return new SimplePrice(true, _originalPrice, _culture, _vat, _vatIncludeInPrice);
            }
        }
        public IPrice WithoutVat
        {
            get
            {
                return new SimplePrice(false, _originalPrice, _culture, _vat, _vatIncludeInPrice);
            }
        }

        public IVatPrice BeforeDiscount
        {
            get;
        }

        public IVatPrice Discount { get; }
        public IPrice Vat { get; }
    }

    public class SimplePrice : IPrice
    {
        private decimal _originalPrice;
        private string _culture;
        private decimal _vat;
        private bool _includeVat;
        private bool _vatIncludeInPrice;
        private Configuration _config;

        public SimplePrice(bool includeVat, decimal originalPrice, string culture, decimal vat, bool vatIncludeInPrice)
        {
            _originalPrice = originalPrice;
            _culture = culture;
            _vat = vat;
            _includeVat = includeVat;
            _config = Configuration.container.GetService<Configuration>();
        }

        public decimal Value
        {
            get
            {
                return GetAmount();
            }
        }
        public string ToCurrencyString()
        {
            var amount = GetAmount();

            return (amount).ToString(_config.CurrencyFormat, new CultureInfo(_culture));
        }

        private decimal GetAmount()
        {
            var price = _originalPrice;

            var vatAmount = price * (_vat / 100m);

            if (_vatIncludeInPrice)
            {
                if (!_includeVat)
                {
                    price = price - vatAmount;
                }

            }
            else
            {
                if (_includeVat)
                {
                    price = price + vatAmount;
                }
            }

            return price;
        }
    }
}
