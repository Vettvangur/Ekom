using Ekom.Interfaces;
using Ekom.Models.Discounts;
using Ekom.Services;
using log4net;
using System.Globalization;
using System.Reflection;

namespace Ekom.Models
{
    class Price : IDiscountedPrice
    {
        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );

        private decimal _originalPrice;
        private string _culture;
        private decimal _vat;
        private bool _vatIncludeInPrice;

        private readonly IPriceCalculationService _priceCalculationService = new PriceCalculationService();

        public IDiscount Discount { get; internal set; }

        public Price(string originalPrice, Store store)
        {
            decimal.TryParse(originalPrice, out decimal result);

            Construct(result, new StoreInfo(store));
        }

        public Price(decimal originalPrice, Store store)
        {
            Construct(originalPrice, new StoreInfo(store));
        }

        public Price(decimal originalPrice, StoreInfo storeInfo)
        {
            Construct(originalPrice, storeInfo);
        }

        private void Construct(decimal originalPrice, StoreInfo storeInfo)
        {
            _originalPrice = originalPrice;
            _vat = storeInfo.Vat;
            _vatIncludeInPrice = storeInfo.VatIncludedInPrice;
            _culture = storeInfo.Culture;
        }

        public decimal OriginalValue => _originalPrice;

        public decimal Value
        {
            get;
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
            _vatIncludeInPrice = vatIncludeInPrice;

            _config = Configuration.container.GetInstance<Configuration>();
        }

        /// <summary>
        /// Value with vat if applicable
        /// </summary>
        public decimal Value
        {
            get
            {
                return GetAmount();
            }
        }
        public string ToCurrencyString
        {
            get
            {
                var amount = GetAmount();
                return amount.ToString(_config.CurrencyFormat, new CultureInfo(_culture));
            }

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

        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );
    }
}
