using Ekom.Interfaces;
using Ekom.Services;
using log4net;
using System.Globalization;
using System.Reflection;

namespace Ekom.Models
{
    class Price : IPrice
    {
        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );

        private string _culture;
        private bool _vatIncludedInPrice;
        private IDiscount _discount;

        private readonly IPriceCalculationService _priceCalculationService = new PriceCalculationService();

        public IDiscount Discount { get; internal set; }

        /// <summary>
        /// Use to ensure that flat discounts are applied before VAT when VAT is included in price.
        /// </summary>
        public bool DiscountAlwaysBeforeVAT { get; internal set; }
        public Price(string originalPrice, IStore store, IDiscount discount = null)
        {
            decimal.TryParse(originalPrice, out decimal result);

            Construct(result, new StoreInfo(store));
        }

        public Price(decimal originalPrice, IStore store, IDiscount discount = null)
        {
            Construct(originalPrice, new StoreInfo(store));
        }

        public Price(decimal originalPrice, StoreInfo storeInfo, IDiscount discount = null)
        {
            Construct(originalPrice, storeInfo, discount);
        }

        private void Construct(decimal originalPrice, StoreInfo storeInfo, IDiscount discount = null)
        {
            OriginalValue = originalPrice;
            Vat = storeInfo.Vat;
            _vatIncludedInPrice = storeInfo.VatIncludedInPrice;
            _culture = storeInfo.Culture;
            _discount = discount;
        }
        private CalculatedPrice CreateSimplePrice(bool includeVat, decimal price, bool isDiscounted = false)
            => new CalculatedPrice(includeVat, price, _culture, Vat, _vatIncludedInPrice, isDiscounted);

        public decimal OriginalValue { get; private set; }
        private ICalculatedPrice _beforeDiscount;
        public ICalculatedPrice BeforeDiscount
            => _beforeDiscount ?? (_beforeDiscount = CreateSimplePrice(includeVat: false, price: OriginalValue));

        private ICalculatedPrice _afterDiscount;
        public ICalculatedPrice AfterDiscount
        {
            get
            {
                if (_afterDiscount != null)
                {
                    return _afterDiscount;
                }

                var price = OriginalValue;

                if (_discount != null)
                {
                    switch (_discount.Amount.Type)
                    {
                        case Discounts.DiscountType.Fixed:

                            if (DiscountAlwaysBeforeVAT && _vatIncludedInPrice)
                            {
                                price /= Vat;
                            }
                            price -= _discount.Amount.Amount;
                            if (DiscountAlwaysBeforeVAT && _vatIncludedInPrice)
                            {
                                price *= Vat;
                            }
                            break;

                        case Discounts.DiscountType.Percentage:

                            price -= price * _discount.Amount.Amount;
                            break;
                    }

                    return _afterDiscount = CreateSimplePrice(includeVat: false, price: price, isDiscounted: true);
                }

                return _afterDiscount = CreateSimplePrice(includeVat: false, price: price);
            }
        }
        public ICalculatedPrice WithoutVat => AfterDiscount;

        public decimal Value => WithVat.Value;
        private ICalculatedPrice _withVat;
        public ICalculatedPrice WithVat
            => _withVat ?? (_withVat = CreateSimplePrice(includeVat: true, price: AfterDiscount.Value, isDiscounted: AfterDiscount.IsDiscounted));

        public decimal Vat { get; private set; }
    }

    /// <summary>
    /// An object that contains the calculated price given the provided parameters
    /// Also offers a way of printing the value using the provided culture.
    /// </summary>
    class CalculatedPrice : ICalculatedPrice, IVatPrice
    {
        private decimal _price;
        private string _culture;
        private decimal _vat;
        private bool _includeVat;
        private bool _vatIncludeInPrice;

        public CalculatedPrice(
            bool includeVat,
            decimal price,
            string culture,
            decimal vat,
            bool vatIncludeInPrice,
            bool isDiscounted)
        {
            _price = price;
            _culture = culture;
            _vat = vat;
            _includeVat = includeVat;
            _vatIncludeInPrice = vatIncludeInPrice;
            IsDiscounted = isDiscounted;
        }

        /// <summary>
        /// Value with vat if applicable
        /// </summary>
        public decimal Value
        {
            get
            {
                var price = _price;

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

        public string ToCurrencyString
        {
            get
            {
                return Value.ToString(Configuration.Current.CurrencyFormat, new CultureInfo(_culture));
            }
        }
        /// <summary>
        /// Used in <see cref="OrderInfo"/> calculations to determine 
        /// if the <see cref="OrderLine"/> already has a discount present
        /// </summary>
        public bool IsDiscounted { get; }

        public ICalculatedPrice WithVat => throw new System.NotImplementedException();

        public ICalculatedPrice WithoutVat => throw new System.NotImplementedException();

        public decimal Vat => throw new System.NotImplementedException();

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
