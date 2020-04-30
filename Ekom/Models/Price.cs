using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.OrderedObjects;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;

namespace Ekom.Models
{
    /// <summary>
    /// Price of item including all data to fully calculate 
    /// before and after VAT/Discount.
    /// </summary>
    public class Price : IPrice
    {
        private bool _hasProductDiscount { get; set; }
        private bool _hasOrderDiscount { get; set; }
        public OrderedProductDiscount ProductDiscount { get; }
        /// <summary>
        /// 
        /// </summary>
        public bool UseOrderDiscount { get; }
        public OrderedDiscount Discount { get; }

        private decimal _Vat { get; }
        private bool _VatIncludedInPrices { get; }
        public CurrencyModel Currency { get; }

        public bool HasProductDiscount => _hasProductDiscount;
        public bool HasOrderDiscount => _hasOrderDiscount;
        /// <summary>
        /// Use to ensure that flat discounts are applied before VAT when VAT is included in price.
        /// </summary>
        public bool DiscountAlwaysBeforeVAT { get; }

        /// <summary>
        /// ctor from JObject
        /// </summary>
        public Price(
            JToken jObject,
            CurrencyModel currency,
            decimal vat,
            bool vatIncludedInPrice,
            OrderedProductDiscount productDiscount = null
        )
        {
            Currency = currency;
            _Vat = vat;
            _VatIncludedInPrices = vatIncludedInPrice;
            OriginalValue = jObject["OriginalValue"].Value<decimal>();
            Discount = jObject["Discount"]?.ToObject<OrderedDiscount>();
            ProductDiscount = productDiscount == null ? jObject["ProductDiscount"]?.ToObject<OrderedProductDiscount>() : productDiscount;
            UseOrderDiscount = jObject["UseOrderDiscount"] != null ? jObject["UseOrderDiscount"].Value<bool>() : false;
            Quantity = jObject["Quantity"] != null ? jObject["Quantity"].Value<int>() : 1;
            DiscountAlwaysBeforeVAT = jObject["DiscountAlwaysBeforeVAT"] != null ? jObject["DiscountAlwaysBeforeVAT"].Value<bool>() : false;
        }
        /// <summary>
        /// ctor
        /// </summary>
        public Price(
            string price,
            CurrencyModel currency,
            decimal vat,
            bool vatIncludedInPrice,
            OrderedProductDiscount productDiscount = null,
            OrderedDiscount discount = null,
            bool useOrderDiscount = true,
            decimal? TotalOrderAmount = null,
            int quantity = 1,
            bool discountAlwaysBeforeVat = false

        )
            : this(decimal.Parse(string.IsNullOrEmpty(price) ? "0" : price.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture),
                 currency,
                 vat,
                 vatIncludedInPrice,
                 productDiscount,
                 discount,
                 useOrderDiscount,
                 TotalOrderAmount,
                 quantity,
                 discountAlwaysBeforeVat)
        {
        }

        /// <summary>
        /// ctor
        /// </summary>
        public Price(
            decimal price,
            CurrencyModel currency,
            decimal vat,
            bool vatIncludedInPrice,
            OrderedProductDiscount productDiscount = null,
            OrderedDiscount discount = null,
            bool useOrderDiscount = true,
            decimal? TotalOrderAmount = null,
            int quantity = 1,
            bool discountAlwaysBeforeVat = false
        )
        {
            OriginalValue = price;
            Currency = currency;
            _Vat = vat;
            _VatIncludedInPrices = vatIncludedInPrice;
            Discount = discount;
            ProductDiscount = productDiscount;
            UseOrderDiscount = useOrderDiscount;
            Quantity = quantity;
            DiscountAlwaysBeforeVAT = discountAlwaysBeforeVat;
        }

        private CalculatedPrice CreateSimplePrice(decimal price)
            => new CalculatedPrice(price, Currency);

        /// <summary>
        /// Simple <see cref="ICloneable"/> implementation using object.MemberwiseClone
        /// </summary>
        /// <returns></returns>
        public object Clone() => MemberwiseClone();

        /// <summary>
        /// Original value with Vat as-is
        /// </summary>
        public decimal OriginalValue { get; }
        /// <summary>
        /// Multiplier
        /// </summary>
        public int Quantity { get; }

        private ICalculatedPrice _beforeDiscount;
        /// <summary>
        /// Price before discount with VAT left as-is
        /// </summary>
        public ICalculatedPrice BeforeDiscount
            => CreateSimplePrice(OriginalValue * Quantity);

        private ICalculatedPrice _afterDiscount;
        /// <summary>
        /// Price after discount with VAT left as-is
        /// </summary>
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
                            var price = CalculateDiscount();

                            _afterDiscount = CreateSimplePrice(price * Quantity);
                        }
                    }
                }

                return _afterDiscount;
            }
        }

        private ICalculatedPrice _withoutVat;
        /// <summary>
        /// Price with discount but without VAT
        /// We cannot depend on AfterDiscount since if rounding is to be applied, 
        /// it should be applied before multiplying by quantity.
        /// Otherwise we would end up with inconsistencies between orderlines of same product but differing quantities.
        /// </summary>
        public ICalculatedPrice WithoutVat
        {
            get
            {
                // http://csharpindepth.com/Articles/General/Singleton.aspx
                // Third version - attempted thread-safety using double-check locking
                if (_withoutVat == null)
                {
                    lock (this)
                    {
                        if (_withoutVat == null)
                        {

                            var price = CalculateDiscount();
                            if (_VatIncludedInPrices)
                            {
                                price = VatCalculator.WithoutVat(price, _Vat, Currency.ISOCurrencySymbol);
                            }

                            _withoutVat = CreateSimplePrice(price * Quantity);
                        }
                    }
                }

                return _withoutVat;
            }
        }

        /// <summary>
        /// Value with discount and VAT
        /// </summary>
        public decimal Value => WithVat.Value;

        private ICalculatedPrice _withVat;
        /// <summary>
        /// Price with discount and VAT.
        /// We cannot depend on AfterDiscount since if rounding is to be applied, 
        /// it should be applied before multiplying by quantity.
        /// Otherwise we would end up with inconsistencies between orderlines of same product but differing quantities.
        /// </summary>
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
                            var price = CalculateDiscount();
                            if (!_VatIncludedInPrices)
                            {
                                price = VatCalculator.WithVat(price, _Vat, Currency.ISOCurrencySymbol);
                            }

                            _withVat = CreateSimplePrice(price * Quantity);
                        }
                    }
                }
                return _withVat;
            }
        }

        /// <summary>
        /// VAT included or to be included in price with discount
        /// </summary>
        public ICalculatedPrice Vat
            => CreateSimplePrice(WithVat.Value - WithoutVat.Value);

        /// <summary>
        /// Total monetary value of discount in price
        /// </summary>
        public ICalculatedPrice DiscountAmount
            => CreateSimplePrice(BeforeDiscount.Value - AfterDiscount.Value);
        private decimal CalculateDiscount()
        {
            if (ProductDiscount == null && Discount == null)
            {
                return OriginalValue;
            }

            var price = OriginalValue;

            if (Discount != null && UseOrderDiscount && ProductDiscount == null)
            {
                price = CalculateOrderDiscount(price);
            }
            else if (Discount != null && !UseOrderDiscount && ProductDiscount == null)
            {
                return price;
            }
            else if (Discount == null && ProductDiscount != null)
            {
                price = CalcualteProductDiscount(price);
            }
            else
            {
                var productDiscountPrice = CalcualteProductDiscount(price);

                if (UseOrderDiscount)
                {
                    if (Discount.Stackable)
                    {
                        return CalculateOrderDiscount(productDiscountPrice);
                    }

                    var OrderDiscountPrice = CalculateOrderDiscount(price);

                    if (productDiscountPrice > OrderDiscountPrice)
                    {
                        _hasProductDiscount = false;
                        return OrderDiscountPrice;
                    }
                    else
                    {
                        _hasOrderDiscount = false;
                        return productDiscountPrice;
                    }
                }
                else
                {
                    return productDiscountPrice;
                }

            }
            return price;

        }

        private decimal CalcualteProductDiscount(decimal price)
        {
            switch (ProductDiscount.Type)
            {
                case Discounts.DiscountType.Fixed:
                    if (DiscountAlwaysBeforeVAT && _VatIncludedInPrices)
                    {
                        price = VatCalculator.WithoutVat(price, _Vat, Currency.ISOCurrencySymbol);
                    }
                    if (ProductDiscount.StartOfRange > price && ProductDiscount.StartOfRange != 0)
                    {
                        break;
                    }
                    if (ProductDiscount.EndOfRange != 0 && ProductDiscount.EndOfRange < price)
                    {
                        break;
                    }
                    price -= ProductDiscount.Discount;
                    _hasProductDiscount = true;
                    if (DiscountAlwaysBeforeVAT && _VatIncludedInPrices)
                    {
                        price = VatCalculator.WithVat(price, _Vat, Currency.ISOCurrencySymbol);
                    }
                    break;

                case Discounts.DiscountType.Percentage:

                    price -= price * ProductDiscount.Discount;
                    _hasProductDiscount = true;
                    break;
            }
            return price;
        }

        private decimal CalculateOrderDiscount(decimal price)
        {
            switch (Discount.Amount.Type)
            {
                case Discounts.DiscountType.Fixed:

                    if (DiscountAlwaysBeforeVAT && _VatIncludedInPrices)
                    {
                        price = VatCalculator.WithoutVat(price, _Vat, Currency.ISOCurrencySymbol);
                    }
                    if (Discount.Constraints.StartRange > price && Discount.Constraints.StartRange != 0)
                    {
                        break;
                    }
                    if (Discount.Constraints.EndRange != 0 && Discount.Constraints.EndRange < price)
                    {
                        break;
                    }
                    price -= Discount.Amount.Amount;
                    _hasOrderDiscount = true;
                    if (DiscountAlwaysBeforeVAT && _VatIncludedInPrices)
                    {
                        price = VatCalculator.WithVat(price, _Vat, Currency.ISOCurrencySymbol);
                    }
                    break;

                case Discounts.DiscountType.Percentage:

                    price -= price * Discount.Amount.Amount;
                    _hasOrderDiscount = true;
                    break;
            }
            return price;
        }


    }

    /// <summary>
    /// An object that contains the calculated price given the provided parameters
    /// Also offers a way of printing the value using the provided culture.
    /// </summary>
    class CalculatedPrice : ICalculatedPrice
    {
        //[JsonConstructor]
        //public CalculatedPrice(
        //    string currencyString,
        //    decimal value
        //)
        //{
        //    Value = value;
        //    //CurrencyString = currencyString;
        //}

        private CurrencyModel _currencyCulture;

        public CalculatedPrice(
            decimal price,
            CurrencyModel currencyCulture
)
        {
            Value = price;
            _currencyCulture = currencyCulture;
        }

        /// <summary>
        /// Value with vat if applicable
        /// </summary>
        public decimal Value { get; }

        public string CurrencyString
        {

            get
            {
                return Value.ToString(_currencyCulture.CurrencyFormat, new CultureInfo(_currencyCulture.CurrencyValue));
            }
        }

    }

}
