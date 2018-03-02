using Ekom.Interfaces;
using Ekom.Models.OrderedObjects;
using log4net;
using System;
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
        public OrderedDiscount Discount { get; internal set; }

        /// <summary>
        /// Use to ensure that flat discounts are applied before VAT when VAT is included in price.
        /// </summary>
        public bool DiscountAlwaysBeforeVAT { get; internal set; }

        public Price(string originalPrice, IStore store, OrderedDiscount discount = null)
        {
            decimal.TryParse(originalPrice, out var result);
            Construct(result, new StoreInfo(store), discount);
        }

        public Price(decimal originalPrice, IStore store, OrderedDiscount discount = null)
        {
            Construct(originalPrice, new StoreInfo(store), discount);
        }

        public Price(string originalPrice, StoreInfo storeInfo, OrderedDiscount discount = null)
        {
            decimal.TryParse(originalPrice, out var result);
            Construct(result, storeInfo, discount);
        }

        public Price(decimal originalPrice, StoreInfo storeInfo, OrderedDiscount discount = null)
        {
            Construct(originalPrice, storeInfo, discount);
        }

        private void Construct(decimal originalPrice, StoreInfo storeInfo, OrderedDiscount discount = null)
        {
            OriginalValue = originalPrice;
            Vat = storeInfo.Vat;
            _vatIncludedInPrice = storeInfo.VatIncludedInPrice;
            _culture = storeInfo.Culture;
            Discount = discount;
        }
        private CalculatedPrice CreateSimplePrice(bool includeVat, decimal price, bool isDiscounted = false)
            => new CalculatedPrice(includeVat, price, _culture, Vat, _vatIncludedInPrice, isDiscounted);

        /// <summary>
        /// Simple <see cref="ICloneable"/> implementation using object.MemberwiseClone
        /// </summary>
        /// <returns></returns>
        public object Clone() => MemberwiseClone();

        /// <summary>
        /// Clone ctor
        /// </summary>
        private Price(decimal originalPrice, decimal vat, bool vatIncludedInPrice, string culture, OrderedDiscount discount)
        {
            OriginalValue = originalPrice;
            Vat = vat;
            _vatIncludedInPrice = vatIncludedInPrice;
            _culture = culture;
            Discount = discount;
        }

        public decimal OriginalValue { get; private set; }
        private ICalculatedPrice _beforeDiscount;
        public ICalculatedPrice BeforeDiscount
        {
            get
            {
                // http://csharpindepth.com/Articles/General/Singleton.aspx
                // Third version - attempted thread-safety using double-check locking
                if (_beforeDiscount == null)
                {
                    lock (this)
                    {
                        if (_beforeDiscount == null)
                        {
                            _beforeDiscount = CreateSimplePrice(includeVat: false, price: OriginalValue);
                        }
                    }
                }

                return _beforeDiscount;
            }
        }

        private ICalculatedPrice _afterDiscount;
        public ICalculatedPrice AfterDiscount
        {
            get
            {
                // http://csharpindepth.com/Articles/General/Singleton.aspx
                // Third version - attempted thread-safety using double-check locking
                if (_afterDiscount == null)
                {
                    lock (this)
                    {
                        if (_afterDiscount == null)
                        {
                            var price = OriginalValue;

                            if (Discount != null)
                            {
                                switch (Discount.Amount.Type)
                                {
                                    case Discounts.DiscountType.Fixed:

                                        if (DiscountAlwaysBeforeVAT && _vatIncludedInPrice)
                                        {
                                            price /= Vat;
                                        }
                                        price -= Discount.Amount.Amount;
                                        if (DiscountAlwaysBeforeVAT && _vatIncludedInPrice)
                                        {
                                            price *= Vat;
                                        }
                                        break;

                                    case Discounts.DiscountType.Percentage:

                                        price -= price * Discount.Amount.Amount;
                                        break;
                                }

                                _afterDiscount
                                    = CreateSimplePrice(includeVat: false, price: price, isDiscounted: true);
                            }

                            _afterDiscount = CreateSimplePrice(includeVat: false, price: price);
                        }
                    }
                }

                return _afterDiscount;
            }
        }
        public ICalculatedPrice WithoutVat => AfterDiscount;

        public decimal Value => WithVat.Value;
        private ICalculatedPrice _withVat;
        public ICalculatedPrice WithVat
        {
            get
            {
                // http://csharpindepth.com/Articles/General/Singleton.aspx
                // Third version - attempted thread-safety using double-check locking
                if (_withVat == null)
                {
                    lock (this)
                    {
                        if (_withVat == null)
                        {
                            _withVat = CreateSimplePrice(
                                includeVat: true,
                                price: AfterDiscount.Value,
                                isDiscounted: AfterDiscount.IsDiscounted);

                        }
                    }
                }

                return _withVat;
            }
        }

        public decimal Vat { get; private set; }
    }

    /// <summary>
    /// An object that contains the calculated price given the provided parameters
    /// Also offers a way of printing the value using the provided culture.
    /// </summary>
    class CalculatedPrice : ICalculatedPrice
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

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
