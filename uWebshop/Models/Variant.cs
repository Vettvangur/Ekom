using Examine;
using log4net;
using Microsoft.Practices.Unity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using uWebshop.API;
using uWebshop.App_Start;
using uWebshop.Cache;
using uWebshop.Helpers;
using uWebshop.Interfaces;
using uWebshop.Services;
using uWebshop.Utilities;

namespace uWebshop.Models
{
    public class Variant : IVariant
    {
        private IPerStoreCache<VariantGroup> _variantGroupCache
        {
            get
            {
                return UnityConfig.GetConfiguredContainer().Resolve<IPerStoreCache<VariantGroup>>();
            }
        }

        private Store _store;
        [JsonIgnore]
        public int Id
        {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("id"));
            }
        }

        [JsonIgnore]
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

        [JsonIgnore]
        public string SKU
        {
            get
            {
                return Properties.GetPropertyValue("sku");
            }
        }

        [JsonIgnore]
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

        [JsonIgnore]
        public decimal OriginalPrice
        {
            get
            {
                var priceField = Properties.GetStoreProperty("price", _store.Alias);

                decimal originalPrice = 0;
                decimal.TryParse(priceField, out originalPrice);

                return originalPrice;
            }
        }

        [JsonIgnore]
        public int Stock
        {
            get
            {
                return 0;
            }
        }

        public Guid ProductKey {
            get
            {
                var paths = Path.Split(',');

                int productId = Convert.ToInt32(paths[paths.Length - 3]);

                var product = Catalog.Instance.GetProduct(Store.Alias, productId);

                if (product == null)
                {
                    throw new Exception("Variant ProductKey could not be created. Product not found. Key: " + productId);
                }

                return product.Key;
            }
        }

        public Guid VariantGroupKey {
            get
            {
                var group = VariantGroup();

                if (group != null) {
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

        [JsonIgnore]
        public Store Store
        {
            get
            {
                return _store;
            }
        }

        [JsonIgnore]
        public int Level
        {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("level"));
            }
        }
        [JsonIgnore]
        public string ContentTypeAlias
        {
            get
            {
                return Properties.GetPropertyValue("nodeTypeAlias");
            }
        }
        [JsonIgnore]
        public int SortOrder
        {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("sortOrder"));
            }
        }
        [JsonIgnore]
        public DateTime CreateDate
        {
            get
            {
                return ExamineHelper.ConvertToDatetime(Properties.GetPropertyValue("createDate"));
            }
        }
        [JsonIgnore]
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
