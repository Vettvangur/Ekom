using Ekom.Interfaces;
using Ekom.Models.Discounts;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ekom.Models
{
    /// <summary>
    /// 
    /// </summary>
    class OrderLine : IOrderLine
    {
        private Guid _productId;
        private IEnumerable<Guid> _variantIds;
        private StoreInfo _storeInfo;

        /// <summary>
        /// 
        /// </summary>
        public Guid Id { get; internal set; }

        public OrderedProduct Product { get; internal set; }
        /// <summary>
        /// 
        /// </summary>
        public int Quantity { get; internal set; }

        /// <summary>
        /// </summary>
        public IDiscount Discount { get; internal set; }
        /// <summary>
        /// 
        /// </summary>
        public string Coupon { get; internal set; }

        /// <summary>
        /// Price
        /// </summary>
        public IPrice Amount
        {
            get
            {

                decimal _price = Product.OriginalPrice;

                if (Product.VariantGroups.Any() && Product.VariantGroups.Any(x => x.Variants.Any()))
                {
                    foreach (var v in Product.VariantGroups.SelectMany(x => x.Variants))
                    {
                        _price = _price + (v.OriginalPrice - _price);
                    }
                }

                return new Price(_price * Quantity, _storeInfo);
            }
        }
        /// <summary>
        /// ctor
        /// </summary>
        public OrderLine(Guid lineId, int quantity, string productJson, StoreInfo storeInfo)
        {
            Id = lineId;
            Quantity = quantity;
            _storeInfo = storeInfo;
            Product = new OrderedProduct(productJson, storeInfo);
        }

        /// <summary>
        /// ctor
        /// </summary>
        public OrderLine(Guid productId, IEnumerable<Guid> variantIds, int quantity, Guid lineId, Store store)
        {
            _productId = productId;
            _variantIds = variantIds;
            _storeInfo = new StoreInfo(store);
            Quantity = quantity;
            Id = lineId;
            Product = new OrderedProduct(productId, variantIds, store);
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
