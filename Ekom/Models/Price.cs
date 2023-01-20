using Ekom.Utilities;
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
        public OrderedDiscount Discount { get; }

        private decimal _storeVAT { get; }
        private bool _storeVatIncludedInPrices { get; }
        public CurrencyModel Currency { get; }

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
            bool vatIncludedInPrice
        )
        {
            Currency = currency;
            _storeVAT = vat;
            _storeVatIncludedInPrices = vatIncludedInPrice;
            OriginalValue = jObject[nameof(OriginalValue)].Value<decimal>();
            Discount = jObject[nameof(Discount)]?.ToObject<OrderedDiscount>();
            Quantity = jObject[nameof(Quantity)]?.Value<int>() ?? 1;
            DiscountAlwaysBeforeVAT = jObject[nameof(DiscountAlwaysBeforeVAT)]?.Value<bool>() ?? false;
        }
        /// <summary>
        /// ctor
        /// </summary>
        public Price(
            string price,
            CurrencyModel currency,
            decimal vat,
            bool vatIncludedInPrice,
            OrderedDiscount discount = null,
            int quantity = 1,
            bool discountAlwaysBeforeVat = false

        )
            : this(
                decimal.Parse(
                    string.IsNullOrEmpty(price)
                        ? "0"
                        : price?.Replace(',', '.') ?? "0",
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture),
                currency,
                vat,
                vatIncludedInPrice,
                discount,
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
            OrderedDiscount discount = null,
            int quantity = 1,
            bool discountAlwaysBeforeVat = false
        )
        {
            OriginalValue = price;
            Currency = currency;
            _storeVAT = vat;
            _storeVatIncludedInPrices = vatIncludedInPrice;
            Discount = discount;
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

        /// <summary>
        /// Price before discount with VAT left as-is
        /// </summary>
        public ICalculatedPrice BeforeDiscount
            => CreateSimplePrice(OriginalValue * Quantity);

        /// <summary>
        /// Price after discount with VAT left as-is
        /// </summary>
        public ICalculatedPrice AfterDiscount
            => CreateSimplePrice(DiscountedValue * Quantity);

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
                var price = DiscountedValue;

                if (_storeVatIncludedInPrices)
                {
                    price = Calculator.WithoutVat(price, _storeVAT, Currency.ISOCurrencySymbol);
                }

                return CreateSimplePrice(price * Quantity);
            }
        }

        /// <summary>
        /// Value with discount and VAT
        /// </summary>
        public decimal Value => WithVat.Value;

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
                var price = DiscountedValue;

                if (!_storeVatIncludedInPrices)
                {
                    price = Calculator.WithVat(price, _storeVAT, Currency.ISOCurrencySymbol);
                }

                return CreateSimplePrice(price * Quantity);
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

        private decimal DiscountedValue
        {
            get
            {
                var price = OriginalValue;

                if (Discount != null)
                {
                    switch (Discount.Type)
                    {
                        case DiscountType.Fixed:

                            if (DiscountAlwaysBeforeVAT && _storeVatIncludedInPrices)
                            {
                                price = Calculator.WithoutVat(price, _storeVAT, Currency.ISOCurrencySymbol);
                            }
                            price -= Discount.Amount;
                            if (DiscountAlwaysBeforeVAT && _storeVatIncludedInPrices)
                            {
                                price = Calculator.WithVat(price, _storeVAT, Currency.ISOCurrencySymbol);
                            }
                            break;

                        case DiscountType.Percentage:

                            price -= price * Discount.Amount;
                            break;
                    }
                }

                return price;
            }
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
                var ci = new CultureInfo(_currencyCulture.CurrencyValue);
                ci = ci.TwoLetterISOLanguageName == "is" ? Configuration.IsCultureInfo : ci;
                return Value.ToString(_currencyCulture.CurrencyFormat, ci);
            }
        }

    }

}
