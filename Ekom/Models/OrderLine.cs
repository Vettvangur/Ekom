using Ekom.Interfaces;
using Ekom.Models.OrderedObjects;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace Ekom.Models
{
    /// <summary>
    /// 
    /// </summary>
    class OrderLine : IOrderLine
    {
        public Guid ProductKey => Product.Key;

        [ScriptIgnore]
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
        /// Line price with discount and quantity and variant modifications
        /// </summary>
        public IPrice Amount
        {
            get
            {

                decimal _price = Product.Price.OriginalValue;
                decimal _totalOriginalPrice = Product.Price.OriginalValue * Quantity;
                if (Product.VariantGroups.Any() && Product.VariantGroups.Any(x => x.Variants.Any()))
                {
                    foreach (var v in Product.VariantGroups.SelectMany(x => x.Variants))
                    {
                        _price = _price + (v.Price.OriginalValue - _price);
                    }
                }

                return new Price(_price, Product.Price.Store, Product.ProductDiscount, Discount, false, _totalOriginalPrice, Quantity);
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
            OrderedDiscount discount)
        {
            Key = lineId;
            Quantity = quantity;
            OrderInfo = orderInfo;
            OrderLineInfo = orderLineInfo;
            Product = new OrderedProduct(productJson, orderInfo.StoreInfo);
            Discount = discount;
        }

        /// <summary>
        /// ctor
        /// </summary>
        public OrderLine(
            IProduct product,
            int quantity,
            Guid lineId,
            OrderInfo orderInfo,
            IVariant variant = null)
        {
            OrderInfo = orderInfo;
            Quantity = quantity;
            Key = lineId;
            Product = new OrderedProduct(product, variant, orderInfo.StoreInfo);
        }
    }
}
