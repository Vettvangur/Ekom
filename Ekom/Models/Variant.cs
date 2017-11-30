using Ekom.API;
using Ekom.Cache;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Utilities;
using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    public class Variant : NodeEntity, IVariant, INodeEntity
    {
        private IPerStoreCache<Category> __categoryCache;
        private IPerStoreCache<Category> _categoryCache =>
            __categoryCache ?? (__categoryCache = Configuration.container.GetInstance<IPerStoreCache<Category>>());

        private IPerStoreCache<VariantGroup> __variantGroupCache;
        private IPerStoreCache<VariantGroup> _variantGroupCache =>
            __variantGroupCache ?? (__variantGroupCache = Configuration.container.GetInstance<IPerStoreCache<VariantGroup>>());

        private Store _store;

        /// <summary>
        /// Stock Keeping Unit, identifier
        /// </summary>
        public string SKU
        {
            get
            {
                return Properties.GetPropertyValue("sku");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        [JsonIgnore]
        public int Stock => API.Stock.Current.GetStock(Key);

        public Product Product
        {
            get
            {
                var productId = ProductId;

                var product = Catalog.Current.GetProduct(_store.Alias, productId);

                if (product == null)
                {
                    throw new Exception("Variant ProductKey could not be created. Product not found. Key: " + productId);
                }

                return product;
            }
        }

        public int ProductId
        {
            get
            {
                var paths = Path.Split(',');

                int productId = Convert.ToInt32(paths[paths.Length - 3]);

                return productId;
            }
        }

        public Guid ProductKey
        {
            get
            {
                return Product.Key;
            }
        }

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

        [JsonIgnore]
        public VariantGroup VariantGroup
        {
            get
            {
                var parentId = Properties.GetPropertyValue("parentID");

                if (int.TryParse(parentId, out int _parentId))
                {
                    var group = _variantGroupCache.Cache[_store.Alias]
                        .Where(x => x.Value.Id == _parentId)
                        .Select(x => x.Value);

                    if (group != null && group.Any())
                    {
                        return group.First();
                    }
                }

                return null;
            }
        }

        [JsonIgnore]
        public Store Store
        {
            get
            {
                return _store;
            }
        }

        public IDiscountedPrice Price
        {
            get
            {
                var variantPrice = Properties.GetStoreProperty("price", _store.Alias);

                if (string.IsNullOrEmpty(variantPrice) || variantPrice == "0")
                {
                    return Product.Price;
                }

                return new Price(variantPrice, _store);
            }
        }

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

            var primaryCategory = _categoryCache.Cache[_store.Alias]
                                                .FirstOrDefault(x => x.Value.Id == categoryId)
                                                .Value;

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
                        = _categoryCache.Cache[_store.Alias]
                                        .FirstOrDefault(x => x.Value.Id == intCatId)
                                        .Value;

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
        public Variant(Store store)
        {
            _store = store;
        }

        /// <summary>
        /// Construct Variant from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public Variant(SearchResult item, Store store) : base(item)
        {
            _store = store;
        }

        /// <summary>
        /// Construct Variant from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public Variant(IContent node, Store store) : base(node)
        {

            _store = store;
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
