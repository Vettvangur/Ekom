using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.OrderedObjects;
using log4net;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Reflection;

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
        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );

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
            : this(decimal.Parse(string.IsNullOrEmpty(price) ? "0" : price.Replace(',','.'), NumberStyles.Any, CultureInfo.InvariantCulture),
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
            => new CalculatedPrice(price, Store.Currency);

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
                                switch (Discount.Amount.Type)
                                {
                                    case Discounts.DiscountType.Fixed:

                                        if (DiscountAlwaysBeforeVAT && Store.VatIncludedInPrice)
                                        {
                                            price = VatCalculator.WithoutVat(price, Store.Vat);
                                        }
                                        price -= Discount.Amount.Amount;
                                        if (DiscountAlwaysBeforeVAT && Store.VatIncludedInPrice)
                                        {
                                            price = VatCalculator.WithVat(price, Store.Vat);
                                        }
                                        break;

                                    case Discounts.DiscountType.Percentage:

                                        price -= price * Discount.Amount.Amount;
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
                                switch (Discount.Amount.Type)
                                {
                                    case Discounts.DiscountType.Fixed:

                                        if (DiscountAlwaysBeforeVAT && Store.VatIncludedInPrice)
                                        {
                                            price = VatCalculator.WithoutVat(price, Store.Vat);
                                        }
                                        price -= Discount.Amount.Amount;
                                        if (DiscountAlwaysBeforeVAT && Store.VatIncludedInPrice)
                                        {
                                            price = VatCalculator.WithVat(price, Store.Vat);
                                        }
                                        break;

                                    case Discounts.DiscountType.Percentage:

                                        price -= price * Discount.Amount.Amount;
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
                                switch (Discount.Amount.Type)
                                {
                                    case Discounts.DiscountType.Fixed:

                                        if (DiscountAlwaysBeforeVAT && Store.VatIncludedInPrice)
                                        {
                                            price = VatCalculator.WithoutVat(price, Store.Vat);
                                        }
                                        price -= Discount.Amount.Amount;
                                        if (DiscountAlwaysBeforeVAT && Store.VatIncludedInPrice)
                                        {
                                            price = VatCalculator.WithVat(price, Store.Vat);
                                        }
                                        break;

                                    case Discounts.DiscountType.Percentage:

                                        price -= price * Discount.Amount.Amount;
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
            string currencyCulture
)
        {
            Value = price;
            if (currencyCulture == "ISK")
            {
                currencyCulture = "is";
            }
            CurrencyString = Value.ToString(Configuration.Current.CurrencyFormat, new CultureInfo(currencyCulture, false));
        }

        /// <summary>
        /// Value with vat if applicable
        /// </summary>
        public decimal Value { get; }

        public string CurrencyString { get; }
    }
}
