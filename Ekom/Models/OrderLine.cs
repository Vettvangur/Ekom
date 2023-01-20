using Ekom.Utilities;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Xml.Serialization;

namespace Ekom.Models
{
    /// <summary>
    /// 
    /// </summary>
    class OrderLine : IOrderLine
    {
        public Guid ProductKey => Product.Key;

        [JsonIgnore]
        [XmlIgnore]
        public IOrderInfo OrderInfo { get; }

        /// <summary>
        /// 
        /// </summary>
        public Guid Key { get; internal set; }

        public OrderedProduct Product { get; internal set; }
        /// <summary>
        /// 
        /// </summary>
        public int Quantity { get; internal set; }
        /// <summary>
        /// Optional Information Attached To Order
        /// </summary>
        public OrderLineInfo OrderLineInfo { get; } = new OrderLineInfo();
        /// <summary>
        /// </summary>
        public OrderedDiscount Discount { get; internal set; }
        /// <summary>
        /// 
        /// </summary>
        public string Coupon { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public OrderLineSettings Settings { get; internal set; } = new OrderLineSettings();

        /// <summary>
        /// Line price with discount and quantity and variant modifications
        /// </summary>
        public IPrice Amount
        {
            get
            {
                decimal _price = Product.Price.Discount != null ? Product.Price.Value : Product.Price.OriginalValue;
                if (Product.VariantGroups.Any() && Product.VariantGroups.Any(x => x.Variants.Any()))
                {
                    foreach (var v in Product.VariantGroups.SelectMany(x => x.Variants))
                    {
                        _price += v.Price.Discount != null ? (v.Price.Value - _price) : (v.Price.OriginalValue - _price);
                    }
                }

                var discount = Discount;
                // This allows us to display discounted prices of orderlines
                // when the order has a global discount applying only to specific DiscountItems
                if (discount == null && OrderInfo.Discount?.DiscountItems.Any() == true)
                {
                    discount = OrderInfo.Discount;
                }

                return new Price(
                    _price,
                    OrderInfo.StoreInfo.Currency,
                    Vat,
                    OrderInfo.StoreInfo.VatIncludedInPrice,
                    discount,
                    Quantity);
            }
        }

        public decimal Vat
        {
            get
            {
                var variantGroup = Product.VariantGroups.FirstOrDefault(x => x.Properties.ContainsKey("vat"));

                if (variantGroup != null && !string.IsNullOrEmpty(variantGroup.Properties.GetPropertyValue("vat", OrderInfo.StoreInfo.Alias)))
                {
                    var vatVal = variantGroup.Properties.GetPropertyValue("vat", OrderInfo.StoreInfo.Alias);
                    return Convert.ToDecimal(vatVal) / 100;
                }
                else
                {
                    return Product.Vat;
                }
            }
        }

        /// <summary>
        /// ctor
        /// </summary>
        public OrderLine(
            Guid lineId,
            int quantity,
            string productJson,
            OrderInfo orderInfo,
            OrderLineInfo orderLineInfo,
            OrderedDiscount discount,
            OrderLineSettings settings)
        {
            Key = lineId;
            Quantity = quantity;
            OrderInfo = orderInfo;
            OrderLineInfo = orderLineInfo;
            Product = new OrderedProduct(productJson, orderInfo.StoreInfo);
            Discount = discount;
            Settings = settings;
        }

        /// <summary>
        /// ctor
        /// </summary>
        public OrderLine(
            IProduct product,
            int quantity,
            Guid lineId,
            OrderInfo orderInfo,
            IVariant variant = null,
            OrderDynamicRequest orderDynamic = null)
        {
            OrderInfo = orderInfo;
            Quantity = quantity;
            Key = lineId;
            Product = new OrderedProduct(product, variant, orderInfo.StoreInfo, orderDynamic);

            if (orderDynamic != null)
            {
                Settings = new OrderLineSettings()
                {
                    CountToTotal = orderDynamic.CountToTotal,
                    Link = orderDynamic.OrderLineLink
                };
            }
        }
    }
}
