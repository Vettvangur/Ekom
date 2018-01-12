using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ekom.Interfaces;
using Ekom.Models.Discounts;
using log4net;

namespace Ekom.Models
{
    public class OrderLine : IOrderLine
    {
        private Guid _productId;
        private IEnumerable<Guid> _variantIds;
        private StoreInfo _storeInfo;

        public Guid Id { get; set; }
        public OrderedProduct Product { get; set; }
        public int Quantity { get; set; }

        /// <summary>
        /// Force changes to come through order api, 
        /// api can then make checks to ensure that a discount is only ever applied to either cart or items, never both.
        /// </summary>
        internal Discount discount;

        public IDiscountedPrice Amount
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
        public OrderLine(Guid lineId, int quantity, string productJson, StoreInfo storeInfo)
        {
            Id = lineId;
            Quantity = quantity;
            _storeInfo = storeInfo;
            Product = new OrderedProduct(productJson, storeInfo);
        }

        public OrderLine(Guid productId, IEnumerable<Guid> variantIds, int quantity, Guid lineId, Store store)
        {
            _productId = productId;
            _variantIds = variantIds;
            _storeInfo = new StoreInfo(store);
            Quantity = quantity;
            Id = lineId;
            Product = new OrderedProduct(productId, variantIds, store);
        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );

    }
}
