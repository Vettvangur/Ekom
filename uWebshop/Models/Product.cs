using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Script.Serialization;
using Umbraco.Core;
using Umbraco.Core.Models;
using uWebshop.Cache;
using uWebshop.Helpers;
using uWebshop.Interfaces;
using uWebshop.Services;

namespace uWebshop.Models
{
    public class Product : IProduct
    {
        private Store _store;
        [JsonIgnore]
        public int Id {
            get
            {
                return Convert.ToInt32(GetPropertyValue("id"));
            }
        }
        [JsonIgnore]
        public Guid Key {
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
        public string Title {
            get
            {
                return Properties.GetStoreProperty("title", _store.Alias);
            }
        }

        /// <summary>
        /// Short spaceless descriptive title used to create URLs
        /// </summary>
        [JsonIgnore]
        public string Slug
        {
            get
            {
                return Properties.GetStoreProperty("slug", _store.Alias);
            }
        }
        [JsonIgnore]
        public decimal OriginalPrice {
            get
            {
                var priceField = Properties.GetStoreProperty("price", _store.Alias);

                decimal originalPrice = 0;
                decimal.TryParse(priceField, out originalPrice);

                return originalPrice;
            }
        }
        [JsonIgnore]
        public int Stock {
            get
            {
                return 0;
            }
        }

        /// <summary>
        /// All categories product belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        [JsonIgnore]
        public List<ICategory> Categories {
            get
            {
                int categoryId = Convert.ToInt32(GetPropertyValue("parentID"));

                var categoryField = Properties.Any(x => x.Key == "categories") ?
                                    GetPropertyValue("categories") : "";

                var categories = new List<ICategory>();

                var primaryCategory = CategoryCache.Cache[_store.Alias]
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
                            = CategoryCache.Cache[_store.Alias]
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

        [JsonIgnore]
        public IEnumerable<Guid> CategoriesIds
        {
            get
            {
                return Categories.Select(x => x.Key);
            }
        }

        [JsonIgnore]
        public Store Store {
            get
            {
                return _store;
            }
        }
        [JsonIgnore]
        public int SortOrder {
            get
            {
                return Convert.ToInt32(GetPropertyValue("sortOrder"));
            }
        }
        [JsonIgnore]
        public int Level {
            get
            {
                return Convert.ToInt32(GetPropertyValue("level"));
            }
        }

        /// <summary>
        /// CSV of node id's describing hierarchy from left to right leading up to node.
        /// </summary>
        [JsonIgnore]
        public string Path
        {
            get
            {
                return GetPropertyValue("path");
            }
        }
        [JsonIgnore]
        public DateTime CreateDate {
            get
            {
                return ExamineHelper.ConvertToDatetime(GetPropertyValue("createDate"));
            }
        }
        [JsonIgnore]
        public DateTime UpdateDate {
            get
            {
                return ExamineHelper.ConvertToDatetime(GetPropertyValue("updateDate"));
            }
        }
        public string Url {
            get {

                var appCache = ApplicationContext.Current.ApplicationCache;
                var r = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

                var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(r.DomainPrefix));

                return findUrlByPrefix ?? Urls.FirstOrDefault();
            }
        }
        [JsonIgnore]
        public string ContentTypeAlias {
            get
            {
                return GetPropertyValue("nodeTypeAlias");
            }
        }
        [JsonIgnore]
        public IEnumerable<string> Urls { get; set; }
        public IDiscountedPrice Price
        {
            get
            {
                return new Price(OriginalPrice, Store);
            }
        }
        [JsonIgnore]
        public IEnumerable<VariantGroup> VariantGroups {
            get
            {
                return VariantGroupCache.Cache[Store.Alias]
                                        .Where(x => x.Value.ProductKey == Key)
                                        .Select(x => x.Value);
            }
        }
        [JsonIgnore]
        public IEnumerable<Variant> AllVariants {
            get
            {
                return VariantCache.Cache[Store.Alias]
                    .Where(x => x.Value.ProductKey == Key)
                    .Select(x => x.Value);
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

            return null;
        }

        public Product(): base() { }
        public Product(SearchResult item, Store store)
        {
            _store = store;

            foreach (var field in item.Fields.Where(x => !x.Key.Contains("__")))
            {
                Properties.Add(field.Key, field.Value);
            }

            Urls = UrlService.BuildProductUrls(Slug, Categories, store);

            if (!Urls.Any() || string.IsNullOrEmpty(Title))
            {
                throw new Exception("No url's or no title present in product");
            }
        }

        public Product(IContent node, Store store)
        {
            try
            {
                _store = store;

                Properties = CreateDefaultUmbracoProperties(node);

                foreach (var prop in node.Properties)
                {
                    Properties.Add(prop.Alias, prop.Value.ToString());
                }


                Urls = UrlService.BuildProductUrls(Slug, Categories, store);

                if (!Urls.Any() || string.IsNullOrEmpty(Title))
                {
                    throw new Exception("No url's or no title present in product");
                }

            } catch(Exception ex)
            {
                Log.Error("Failed to create product. ", ex );
            }

        }

        public static Dictionary<string, string> CreateDefaultUmbracoProperties(IContent node)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "id",
                    node.Id.ToString()
                },
                {
                    "key",
                    node.Key.ToString()
                },
                {
                    "path",
                    node.Path
                },
                {
                    "level",
                    node.Level.ToString()
                },
                {
                    "sortOrder",
                    node.SortOrder.ToString()
                },
                {
                    "parentID",
                    node.ParentId.ToString()
                },
                {
                    "writerID",
                    node.WriterId.ToString()
                },
                {
                    "creatorID",
                    node.CreatorId.ToString()
                },
                {
                    "nodeTypeAlias",
                    node.ContentType.Alias
                },
                {
                    "updateDate",
                    node.UpdateDate.ToString("yyyyMMddHHmmssfff")
                },
                {
                    "createDate",
                    node.CreateDate.ToString("yyyyMMddHHmmssfff")
                }
            };

            return properties;
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
