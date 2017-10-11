using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using Umbraco.Core.Models;
using uWebshop.API;
using uWebshop.Cache;
using uWebshop.Helpers;
using uWebshop.Interfaces;
using uWebshop.Utilities;

namespace uWebshop.Models
{
    public class Variant : NodeEntity, IVariant, INodeEntity
    {
        private IPerStoreCache<Category> __categoryCache;
        private IPerStoreCache<Category> _categoryCache => 
            __categoryCache ?? (__categoryCache = Configuration.container.GetService<IPerStoreCache<Category>>());

        private IPerStoreCache<VariantGroup> __variantGroupCache;
        private IPerStoreCache<VariantGroup> _variantGroupCache => 
            __variantGroupCache ?? (__variantGroupCache = Configuration.container.GetService<IPerStoreCache<VariantGroup>>());

        private Store _store;

        public string SKU
        {
            get
            {
                return Properties.GetPropertyValue("sku");
            }
        }

        public string Title
        {
            get
            {
                return Properties.GetStoreProperty("title", _store.Alias);
            }
        }

        private decimal? _originalPrice;
        public decimal OriginalPrice
        {
            get
            {
                decimal originalPrice = 0;

                if (_originalPrice.HasValue)
                {
                    originalPrice = _originalPrice.Value;
                }
                else
                {
                    var priceField = Properties.GetStoreProperty("price", _store.Alias);


                    decimal.TryParse(priceField, out originalPrice);
                }

                return originalPrice;
            }
            set
            {
                _originalPrice = value;
            }
        }

        public int Stock
        {
            get
            {
                return 0;
            }
        }

        public Guid ProductKey
        {
            get
            {
                var paths = Path.Split(',');

                int productId = Convert.ToInt32(paths[paths.Length - 3]);

                var product = Catalog.Current.GetProduct(_store.Alias, productId);

                if (product == null)
                {
                    throw new Exception("Variant ProductKey could not be created. Product not found. Key: " + productId);
                }

                return product.Key;
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

        public IDiscountedPrice Price
        {
            get
            {
                return new Price(OriginalPrice, _store);
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
        /// Used by uWebshop extensions
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
