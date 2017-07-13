using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using uWebshop.Cache;
using uWebshop.Helpers;
using uWebshop.Interfaces;
using uWebshop.Services;

namespace uWebshop.Models
{
    public class Variant : IVariant
    {
        private Store _store;
        [JsonIgnore]
        public int Id
        {
            get
            {
                return Convert.ToInt32(GetPropertyValue("id"));
            }
        }

        [JsonIgnore]
        public Guid Key
        {
            get
            {
                var key = GetPropertyValue("key");

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
                return GetPropertyValue("sku");
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
                return GetPropertyValue("path");
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

                var product = API.Catalog.GetProduct(Store.Alias, productId);

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
            var parentId = GetPropertyValue("parentID");
            int _parentId = 0;

            if (Int32.TryParse(parentId, out _parentId))
            {
                var group = VariantGroupCache.Cache[Store.Alias]
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
                return Convert.ToInt32(GetPropertyValue("level"));
            }
        }
        [JsonIgnore]
        public string ContentTypeAlias
        {
            get
            {
                return GetPropertyValue("nodeTypeAlias");
            }
        }
        [JsonIgnore]
        public int SortOrder
        {
            get
            {
                return Convert.ToInt32(GetPropertyValue("sortOrder"));
            }
        }
        [JsonIgnore]
        public DateTime CreateDate
        {
            get
            {
                return ExamineHelper.ConvertToDatetime(GetPropertyValue("createDate"));
            }
        }
        [JsonIgnore]
        public DateTime UpdateDate
        {
            get
            {
                return ExamineHelper.ConvertToDatetime(GetPropertyValue("updateDate"));
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

        public string GetPropertyValue(string propertyAlias)
        {
            if (!string.IsNullOrEmpty(propertyAlias))
            {
                if (Properties.ContainsKey(propertyAlias))
                {
                    return Properties[propertyAlias];
                }
            }

            return string.Empty;
        }

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
            try
            {
                _store = store;

                foreach (var field in item.Fields.Where(x => !x.Key.Contains("__")))
                {
                    Properties.Add(field.Key, field.Value);
                }

            } catch(Exception ex)
            {
                Log.Error("Failed to create variant from examine. Id: " + item.Id, ex);
            }
        }

        public Variant(IContent node, Store store)
        {
            try
            {
                _store = store;

                Properties = Product.CreateDefaultUmbracoProperties(node);

                foreach (var prop in node.Properties)
                {
                    Properties.Add(prop.Alias, prop.Value.ToString());
                }


            } catch(Exception ex)
            {
                Log.Error("Failed to create variant from content. Id: " + node.Id, ex);
            }

        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
