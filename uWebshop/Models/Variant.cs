using Examine;
using log4net;
using Microsoft.Practices.Unity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Umbraco.Core.Models;
using uWebshop.API;
using uWebshop.App_Start;
using uWebshop.Cache;
using uWebshop.Helpers;
using uWebshop.Interfaces;
using uWebshop.Utilities;

namespace uWebshop.Models
{
    public class Variant : IVariant
    {

        private IPerStoreCache<Category> _categoryCache
        {
            get
            {
                return UnityConfig.GetConfiguredContainer().Resolve<IPerStoreCache<Category>>();
            }
        }

        private IPerStoreCache<VariantGroup> _variantGroupCache
        {
            get
            {
                return UnityConfig.GetConfiguredContainer().Resolve<IPerStoreCache<VariantGroup>>();
            }
        }

        private Store _store;
        public int Id
        {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("id"));
            }
        }

        public Guid Key
        {
            get
            {
                var key = Properties.GetPropertyValue("key");

                var _key = new Guid();

                if (!Guid.TryParse(key, out _key))
                {
                    throw new Exception("No key present for product.");
                }

                return _key;
            }
        }

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

        [JsonIgnore]
        public string Path
        {
            get
            {
                return Properties.GetPropertyValue("path");
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

                var product = Catalog.Current.GetProduct(Store.Alias, productId);

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
                var group = VariantGroup();

                if (group != null)
                {
                    return group.Key;
                }

                return Guid.Empty;
            }
        }

        public VariantGroup VariantGroup()
        {
            var parentId = Properties.GetPropertyValue("parentID");
            int _parentId = 0;

            if (Int32.TryParse(parentId, out _parentId))
            {
                var group = _variantGroupCache.Cache[Store.Alias]
                                        .Where(x => x.Value.Id == _parentId)
                                        .Select(x => x.Value);

                if (group != null && group.Any())
                {
                    return group.First();
                }
            }

            return null;
        }

        public Store Store
        {
            get
            {
                return _store;
            }
        }

        public int Level
        {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("level"));
            }
        }

        public string ContentTypeAlias
        {
            get
            {
                return Properties.GetPropertyValue("nodeTypeAlias");
            }
        }

        public int SortOrder
        {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("sortOrder"));
            }
        }

        public DateTime CreateDate
        {
            get
            {
                return ExamineHelper.ConvertToDatetime(Properties.GetPropertyValue("createDate"));
            }
        }

        public DateTime UpdateDate
        {
            get
            {
                return ExamineHelper.ConvertToDatetime(Properties.GetPropertyValue("updateDate"));
            }
        }
        public IDiscountedPrice Price
        {
            get
            {
                return new Price(OriginalPrice, Store);
            }
        }

        /// <summary>
        /// All categories variant belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        public List<ICategory> Categories
        {
            get
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
        }

        public Dictionary<string, string> Properties = new Dictionary<string, string>();

        /// <summary>
        /// Used by uWebshop extensions
        /// </summary>
        /// <param name="store"></param>
        public Variant(Store store)
        {
            _store = store;
        }

        public Variant(SearchResult item, Store store)
        {
            _store = store;

            foreach (var field in item.Fields.Where(x => !x.Key.Contains("__")))
            {
                Properties.Add(field.Key, field.Value);
            }
        }

        public Variant(IContent node, Store store)
        {
            _store = store;

            Properties = Product.CreateDefaultUmbracoProperties(node);

            foreach (var prop in node.Properties)
            {
                Properties.Add(prop.Alias, prop.Value.ToString());
            }
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
