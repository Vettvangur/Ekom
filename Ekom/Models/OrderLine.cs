﻿using Ekom.Interfaces;
using Ekom.Models.OrderedObjects;
using log4net;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Reflection;
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

                if (Product.VariantGroups.Any() && Product.VariantGroups.Any(x => x.Variants.Any()))
                {
                    foreach (var v in Product.VariantGroups.SelectMany(x => x.Variants))
                    {
                        _price = _price + (v.Price.OriginalValue - _price);
                    }
                }

                return new Price(_price, Product.Price.Store, Discount, Quantity);
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
            OrderedDiscount discount)
        {
            Key = lineId;
            Quantity = quantity;
            OrderInfo = orderInfo;
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

        /// <summary>
        /// 
        /// </summary>
        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
