using Ekom.API;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models.OrderedObjects;
using Ekom.Utilities;
using Examine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// A customization of a parent product, currently must belong to a <see cref="Models.VariantGroup"/>
    /// Price of variant is added to product base price to calculate total price.
    /// Has seperate stock from base product.
    /// </summary>
    public class Variant : PerStoreNodeEntity, IVariant, IPerStoreNodeEntity
    {
        private IPerStoreCache<IVariantGroup> __variantGroupCache;
        private IPerStoreCache<IVariantGroup> _variantGroupCache =>
            __variantGroupCache ?? (__variantGroupCache = Current.Factory.GetInstance<IPerStoreCache<IVariantGroup>>());

        /// <summary>
        /// Stock Keeping Unit, identifier
        /// </summary>
        public string SKU => Properties.GetPropertyValue("sku");

        /// <summary>
        /// 
        /// </summary>
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public virtual int Stock => API.Stock.Instance.GetStock(Key);

        /// <summary>
        /// Parent <see cref="IProduct"/> of Variant
        /// </summary>
        public IProduct Product
        {
            get
            {
                var product = Catalog.Instance.GetProduct(Store.Alias, ProductId);

                if (product == null)
                {
                    throw new KeyNotFoundException("Variant Product not found. Key: " + ProductId);
                }

                return product;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public int ProductId
        {
            get
            {
                var paths = Path.Split(',');

                int productId = Convert.ToInt32(paths[paths.Length - 3]);

                return productId;
            }
        }

        /// <summary>
        /// Get the Product Key
        /// </summary>
        public Guid ProductKey
        {
            get
            {
                return Product.Key;
            }
        }
        /// <summary>
        /// Gets the productDiscount for the specific Variant
        /// </summary>
        public ProductDiscount ProductDiscount
        {
            get
            {
                return Current.Factory.GetInstance<IProductDiscountService>()
                    .GetProductDiscount(
                        Guid.Parse(Properties["key"]),
                        Store.Alias,
                        Properties.GetPropertyValue("price", Store.Alias)
                    );
            }
        }
        // Waiting for variants to be composed with their parent product
        ///// <summary>
        ///// Get the Product Key
        ///// </summary>
        //public Guid ProductKey => Product.Key;
        ///// <summary>
        ///// 
        ///// </summary>
        //public int ProductId => Product.Id;

        /// <summary>
        /// <see cref="IVariantGroup"/> Key
        /// </summary>
        public Guid VariantGroupKey
        {
            get
            {
                var group = VariantGroup;

                if (group != null)
                {
                    return group.Key;
                }

                return Guid.Empty;
            }
        }

        /// <summary>
        /// Variant group <see cref="IVariant"/> belongs to
        /// </summary>
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public IVariantGroup VariantGroup
        {
            get
            {
                var parentId = Properties.GetPropertyValue("parentID");

                if (int.TryParse(parentId, out int _parentId))
                {
                    return Catalog.Instance.GetVariantGroup(Store.Alias, _parentId); 
                }

                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual IPrice Price { get; }

        /// <summary>
        /// All categories variant belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        public List<ICategory> Categories()
        {
            var paths = Path.Split(',');

            int categoryId = Convert.ToInt32(paths[paths.Length - 4]);

            var categoryField = Properties.Any(x => x.Key == "categories") ?
                                Properties.GetPropertyValue("categories") : "";

            var categories = new List<ICategory>();

            var primaryCategory = API.Catalog.Instance.GetCategory(Store.Alias, categoryId);

            if (primaryCategory != null)
            {
                categories.Add(primaryCategory);
            }

            if (!string.IsNullOrEmpty(categoryField))
            {
                var categoryIds = categoryField.Split(',');

                foreach (var catId in categoryIds)
                {
                    var intCatId = Convert.ToInt32(catId);

                    var categoryItem
                        = Catalog.Instance.GetCategory(Store.Alias, intCatId);

                    if (categoryItem != null && !categories.Contains(categoryItem))
                    {
                        categories.Add(categoryItem);
                    }
                }
            }

            return categories;
        }

        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        public Variant(IStore store) : base(store) { }

        /// <summary>
        /// Construct Variant from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public Variant(ISearchResult item, IStore store) : base(item, store)
        {
            var variantPrice = Properties.GetPropertyValue("price", Store.Alias);

            if (string.IsNullOrEmpty(variantPrice) || variantPrice == "0")
            {
                Price = Product.Price;
            }

            Price = new Price(variantPrice, Store ,ProductDiscount == null ? null : new OrderedProductDiscount(ProductDiscount));
        }

        /// <summary>
        /// Construct Variant from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public Variant(IContent node, IStore store) : base(node, store)
        {
            var variantPrice = Properties.GetPropertyValue("price", Store.Alias);

            if (string.IsNullOrEmpty(variantPrice) || variantPrice == "0")
            {
                Price = Product.Price;
            }

            Price = new Price(variantPrice, Store, ProductDiscount == null ? null : new OrderedProductDiscount(ProductDiscount));
        }
    }
}
