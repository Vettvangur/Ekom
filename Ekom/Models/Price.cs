using Ekom.Interfaces;
using Ekom.Models.OrderedObjects;
using Ekom.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;

namespace Ekom.Models
{
    /// <summary>
    /// Price of item including all data to fully calculate 
    /// before and after VAT/Discount.
    /// </summary>
    public class Price : IPrice
    {
        /// <summary>
        /// 
        /// </summary>
        public OrderedDiscount Discount { get; }
        /// <summary>
        /// 
        /// </summary>
        public StoreInfo Store { get; }

        /// <summary>
        /// Use to ensure that flat discounts are applied before VAT when VAT is included in price.
        /// </summary>
        public bool DiscountAlwaysBeforeVAT { get; }

        /// <summary>
        /// Json constructor
        /// </summary>
        [JsonConstructor]
        public Price(
            OrderedDiscount discount,
            StoreInfo store,
            decimal originalValue,
            bool discountAlwaysBeforeVAT,
            int quantity
        )
            : this(originalValue, store, discount, quantity, discountAlwaysBeforeVAT)
        {
        }
        /// <summary>
        /// ctor from JObject
        /// </summary>
        public Price(
            JToken jObject
        )
        {
            var currency = jObject[nameof(Store)]?[nameof(Store.Currency)];
            if (currency != null && currency.Type == JTokenType.String)
            {
                var list = new System.Collections.Generic.List<CurrencyModel>();

                list.Add(new CurrencyModel()
                {
                    CurrencyFormat = "C",
                    CurrencyValue = currency.Value<string>()
                });
                var store = jObject[nameof(Store)];
                var key = new Guid(store[nameof(Store.Key)].Value<string>());
                var culture = store[nameof(Store.Culture)].Value<string>();
                var alias = store[nameof(Store.Alias)].Value<string>();
                var vatincluded = store[nameof(Store.VatIncludedInPrice)].Value<bool>();
                var vat = store[nameof(Store.Vat)].Value<decimal>();
                Store = new StoreInfo(
                    key: key,
                    currency: list,
                    culture: culture,
                    alias: alias,
                    vatIncludedInPrice: vatincluded,
                    vat: vat
                );
            }
            else
            {
                Store = jObject[nameof(Store)]?.ToObject<StoreInfo>();
            }

            OriginalValue = jObject[nameof(OriginalValue)].Value<decimal>();
            Discount = jObject[nameof(Discount)]?.ToObject<OrderedDiscount>();
            Quantity = jObject[nameof(Quantity)] != null ? jObject[nameof(Quantity)].Value<int>() : 1;
            DiscountAlwaysBeforeVAT = jObject[nameof(DiscountAlwaysBeforeVAT)] != null ? jObject[nameof(DiscountAlwaysBeforeVAT)].Value<bool>() : false;
        }
        /// <summary>
        /// ctor
        /// </summary>
        public Price(
            string price,
            IStore store,
            OrderedDiscount discount = null,
            int quantity = 1,
            bool discountAlwaysBeforeVat = false
        )
            : this(decimal.Parse(string.IsNullOrEmpty(price) ? "0" : price.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture),
                 new StoreInfo(store),
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
            IStore store,
            OrderedDiscount discount = null,
            int quantity = 1,
            bool discountAlwaysBeforeVat = false
        )
            : this(price, new StoreInfo(store), discount, quantity, discountAlwaysBeforeVat)
        {
        }

        /// <summary>
        /// ctor
        /// </summary>
        public Price(
            string price,
            StoreInfo storeInfo,
            OrderedDiscount discount = null,
            int quantity = 1,
            bool discountAlwaysBeforeVat = false
        )
            : this(decimal.Parse(string.IsNullOrEmpty(price) ? "0" : price.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture),
                 storeInfo,
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
            StoreInfo storeInfo,
            OrderedDiscount discount = null,
            int quantity = 1,
            bool discountAlwaysBeforeVat = false
        )
        {
            OriginalValue = price;
            Store = storeInfo;
            Discount = discount;
            Quantity = quantity;
            DiscountAlwaysBeforeVAT = discountAlwaysBeforeVat;
        }

        private CalculatedPrice CreateSimplePrice(decimal price)
            => new CalculatedPrice(price, Store.Currency.First());

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
                            var price = OriginalValue;

                            if (Discount != null)
                            {
                                switch (Discount.Type)
                                {
                                    case Discounts.DiscountType.Fixed:

                                        if (DiscountAlwaysBeforeVAT && Store.VatIncludedInPrice)
                                        {
                                            price = VatCalculator.WithoutVat(price, Store.Vat);
                                        }
                                        price -= Discount.Amount;
                                        if (DiscountAlwaysBeforeVAT && Store.VatIncludedInPrice)
                                        {
                                            price = VatCalculator.WithVat(price, Store.Vat);
                                        }
                                        break;

                                    case Discounts.DiscountType.Percentage:

                                        price -= price * Discount.Amount;
                                        break;
                                }
                            }

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
                            var price = OriginalValue;

                            if (Discount != null)
                            {
                                switch (Discount.Type)
                                {
                                    case Discounts.DiscountType.Fixed:

                                        if (DiscountAlwaysBeforeVAT && Store.VatIncludedInPrice)
                                        {
                                            price = VatCalculator.WithoutVat(price, Store.Vat);
                                        }
                                        price -= Discount.Amount;
                                        if (DiscountAlwaysBeforeVAT && Store.VatIncludedInPrice)
                                        {
                                            price = VatCalculator.WithVat(price, Store.Vat);
                                        }
                                        break;

                                    case Discounts.DiscountType.Percentage:

                                        price -= price * Discount.Amount;
                                        break;
                                }
                            }

                            if (Store.VatIncludedInPrice)
                            {
                                price = VatCalculator.WithoutVat(price, Store.Vat);
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
                            var price = OriginalValue;

                            if (Discount != null)
                            {
                                switch (Discount.Type)
                                {
                                    case Discounts.DiscountType.Fixed:

                                        if (DiscountAlwaysBeforeVAT && Store.VatIncludedInPrice)
                                        {
                                            price = VatCalculator.WithoutVat(price, Store.Vat);
                                        }
                                        price -= Discount.Amount;
                                        if (DiscountAlwaysBeforeVAT && Store.VatIncludedInPrice)
                                        {
                                            price = VatCalculator.WithVat(price, Store.Vat);
                                        }
                                        break;

                                    case Discounts.DiscountType.Percentage:

                                        price -= price * Discount.Amount;
                                        break;
                                }
                            }

                            if (!Store.VatIncludedInPrice)
                            {
                                price = VatCalculator.WithVat(price, Store.Vat);
                            }

                            _withVat = CreateSimplePrice(price * Quantity);
                        }
                    }
                }

                return _withVat;
            }
        }

        private ICalculatedPrice _vat;
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
    }

    /// <summary>
    /// An object that contains the calculated price given the provided parameters
    /// Also offers a way of printing the value using the provided culture.
    /// </summary>
    class CalculatedPrice : ICalculatedPrice
    {
        [JsonConstructor]
        public CalculatedPrice(
            string currencyString,
            decimal value
        )
        {
            Value = value;
            CurrencyString = currencyString;
        }

        public CalculatedPrice(
            decimal price,
            CurrencyModel currencyCulture
)
        {
            Value = price;
            //Backwards compatability
            // Commented out after changes but kept in as a reminder incase something breaks.
            //if (currencyCulture == "ISK")
            //{
            //    currencyCulture = "is";
            //}
            if (currencyCulture.CurrencyValue == null)
            {
                currencyCulture.CurrencyValue = "is";
            }
            CurrencyString = Value.ToString(currencyCulture.CurrencyFormat, new CultureInfo(currencyCulture.CurrencyValue));
        }

        /// <summary>
        /// Value with vat if applicable
        /// </summary>
        public decimal Value { get; }

        public string CurrencyString { get; }
    }
}
