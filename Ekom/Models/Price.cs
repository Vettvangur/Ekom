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
    public class Price : IPrice
    {
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
        /// 
        /// </summary>
        [JsonConstructor]
        public Price(
            OrderedDiscount discount,
            StoreInfo store,
            decimal originalValue,
            bool discountAlwaysBeforeVAT,
            int quantity)
            : this(originalValue, store, discount, quantity)
        {
            DiscountAlwaysBeforeVAT = discountAlwaysBeforeVAT;
        }

        public Price(string price, IStore store, OrderedDiscount discount = null, int quantity = 1)
            : this(decimal.Parse(string.IsNullOrEmpty(price) ? "0" : price),
                 new StoreInfo(store),
                 discount,
                 quantity)
        {
        }

        public Price(decimal price, IStore store, OrderedDiscount discount = null, int quantity = 1)
            : this(price, new StoreInfo(store), discount, quantity)
        {
        }

        public Price(string price, StoreInfo storeInfo, OrderedDiscount discount = null, int quantity = 1)
            : this(decimal.Parse(string.IsNullOrEmpty(price) ? "0" : price),
                 storeInfo,
                 discount,
                 quantity)
        {
        }

        public Price(decimal price, StoreInfo storeInfo, OrderedDiscount discount = null, int quantity = 1)
        {
            OriginalValue = price;
            Store = storeInfo;
            Discount = discount;
            Quantity = quantity;
        }

        private CalculatedPrice CreateSimplePrice(decimal price)
            => new CalculatedPrice(price, Store.Culture);

        /// <summary>
        /// Simple <see cref="ICloneable"/> implementation using object.MemberwiseClone
        /// </summary>
        /// <returns></returns>
        public object Clone() => MemberwiseClone();

        public decimal OriginalValue { get; }
        public int Quantity { get; }

        private ICalculatedPrice _beforeDiscount;
        /// <summary>
        /// Price before discount with VAT left as-is
        /// </summary>
        [JsonIgnore]
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
                            _beforeDiscount = CreateSimplePrice(OriginalValue * Quantity);
                        }
                    }
                }

                return _beforeDiscount;
            }
        }

        private ICalculatedPrice _afterDiscount;
        /// <summary>
        /// Price after discount with VAT left as-is
        /// </summary>
        [JsonIgnore]
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
        /// </summary>
        [JsonIgnore]
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
                            decimal price = AfterDiscount.Value;
                            if (Store.VatIncludedInPrice)
                            {
                                price = VatCalculator.WithoutVat(price, Store.Vat);
                            }

                            _withoutVat = CreateSimplePrice(price);
                        }
                    }
                }

                return _withoutVat;
            }
        }

        public decimal Value => WithVat.Value;

        private ICalculatedPrice _withVat;
        /// <summary>
        /// Price with discount and VAT
        /// </summary>
        [JsonIgnore]
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
                            var price = AfterDiscount.Value;
                            if (!Store.VatIncludedInPrice)
                            {
                                price = VatCalculator.WithVat(price, Store.Vat);
                            }

                            _withVat = CreateSimplePrice(price);
                        }
                    }
                }

                return _withVat;
            }
        }

        private ICalculatedPrice _vat;
        /// <summary>
        /// VAT included or to be included in price
        /// </summary>
        [JsonIgnore]
        public ICalculatedPrice Vat
        {
            get
            {
                // http://csharpindepth.com/Articles/General/Singleton.aspx
                // Third version - attempted thread-safety using double-check locking
                if (_vat == null)
                {
                    lock (this)
                    {
                        if (_vat == null)
                        {
                            decimal price = 0;
                            if (Store.VatIncludedInPrice)
                            {
                                price = VatCalculator.VatAmountFromWithVat(OriginalValue, Store.Vat);
                            }
                            else
                            {
                                price = VatCalculator.VatAmountFromWithoutVat(OriginalValue, Store.Vat);
                            }

                            _vat = CreateSimplePrice(price);
                        }
                    }
                }

                return _vat;
            }
        }
    }

    /// <summary>
    /// An object that contains the calculated price given the provided parameters
    /// Also offers a way of printing the value using the provided culture.
    /// </summary>
    class CalculatedPrice : ICalculatedPrice
    {
        public CalculatedPrice(
            decimal price,
            string culture
)
        {
            Value = price;
            CurrencyString = Value.ToString(Configuration.Current.CurrencyFormat, new CultureInfo(culture));
        }

        /// <summary>
        /// Value with vat if applicable
        /// </summary>
        public decimal Value { get; internal set; }

        public string CurrencyString { get; internal set; }
    }
}
