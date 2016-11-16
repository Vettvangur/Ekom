using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.API;
using uWebshop.Cache;
using uWebshop.Services;

namespace uWebshop.Models
{
    public class BasketLine : IEquatable<BasketLine>
    {
        #region Properties

        // A place to store the quantity in the cart
        // This property has an implicit getter and setter.
        public int Quantity { get; set; }

        private int _productId;
        public int ProductId
        {
            get { return _productId; }
            set
            {
                // To ensure that the Prod object will be re-created
                _product = null;
                _productId = value;
            }
        }

        private Product _product = null;
        public Product Product
        {
            get
            {
                // Lazy initialization - the object won't be created until it is needed
                if (_product == null)
                {
                    _product = Catalog.GetProduct(ProductId);
                }

                return _product;
            }
        }

        public int[] VariantIds { get; set; }

        public IEnumerable<Variant> Variants
        {
            get
            {
                var variants = new List<Variant>();

                if (VariantIds != null && VariantIds.Any())
                {
                    foreach (var variantId in VariantIds)
                    {
                        variants.Add(Catalog.GetVariant(Store.Alias, variantId));
                    }
                }

                return variants;
            }
        }

        public Price UnitPrice
        {
            get {
                return null;
            }
        }

        public Price TotalPrice
        {
            get {
                return new Price(UnitPrice.Value * Quantity);
            }
        }

        public Store Store { get; set; }
        #endregion

        public BasketLine(int productId, int[] variantIds)
        {
            var store = StoreService.GetStore();

            this.Store = store;

            this.ProductId = productId;
            this.VariantIds = variantIds;
        }

        /**
         * Equals() - Needed to implement the IEquatable interface
         *    Tests whether or not this item is equal to the parameter
         *    This method is called by the Contains() method in the List class
         *    We used this Contains() method in the ShoppingCart AddItem() method
         */
        public bool Equals(BasketLine item)
        {
            // Need to check Variant to
            return item.ProductId == this.ProductId;
        }
    }
}
