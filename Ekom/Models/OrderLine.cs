using Ekom.Interfaces;
using Ekom.Models.OrderedObjects;
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

                return new Price(_price, _storeInfo, Discount, Quantity);
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
        public OrderLine(Guid productId, IEnumerable<Guid> variantIds, int quantity, Guid lineId, IStore store)
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
