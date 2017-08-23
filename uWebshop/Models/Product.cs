using Examine;
using log4net;
using Microsoft.Practices.Unity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Script.Serialization;
using Umbraco.Core;
using Umbraco.Core.Models;
using uWebshop.App_Start;
using uWebshop.Cache;
using uWebshop.Helpers;
using uWebshop.Interfaces;
using uWebshop.Services;
using uWebshop.Utilities;

namespace uWebshop.Models
{
    public class Product : IProduct
    {
        private IPerStoreCache<Category> _categoryCache
        {
            get
            {
                return UnityConfig.GetConfiguredContainer().Resolve<IPerStoreCache<Category>>();
            }
        }

        private IPerStoreCache<Variant> _variantCache
        {
            get
            {
                return UnityConfig.GetConfiguredContainer().Resolve<IPerStoreCache<Variant>>();
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
        public int Id {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("id"));
            }
        }

        public Guid Key {
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

        public string Title {
            get
            {
                return Properties.GetStoreProperty("title", _store.Alias);
            }
        }

        public string Description
        {
            get
            {
                return Properties.GetStoreProperty("description", _store.Alias);
            }
        }

        public string Summary
        {
            get
            {
                return Properties.GetStoreProperty("summary", _store.Alias);
            }
        }

        /// <summary>
        /// Short spaceless descriptive title used to create URLs
        /// </summary>
        public string Slug
        {
            get
            {
                return Properties.GetStoreProperty("slug", _store.Alias);
            }
        }

        public decimal OriginalPrice {
            get
            {
                var priceField = Properties.GetStoreProperty("price", _store.Alias);

                decimal originalPrice = 0;
                decimal.TryParse(priceField, out originalPrice);

                return originalPrice;
            }
        }

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
        public List<ICategory> Categories {
            get
            {
                int categoryId = Convert.ToInt32(Properties.GetPropertyValue("parentID"));

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

        public IEnumerable<Guid> CategoriesIds
        {
            get
            {
                return Categories.Select(x => x.Key);
            }
        }

        public Store Store {
            get
            {
                return _store;
            }
        }
        public int SortOrder {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("sortOrder"));
            }
        }
        public int Level {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("level"));
            }
        }

        /// <summary>
        /// CSV of node id's describing hierarchy from left to right leading up to node.
        /// </summary>
        public string Path
        {
            get
            {
                return Properties.GetPropertyValue("path");
            }
        }
        public DateTime CreateDate {
            get
            {
                return ExamineHelper.ConvertToDatetime(Properties.GetPropertyValue("createDate"));
            }
        }
        public DateTime UpdateDate {
            get
            {
                return ExamineHelper.ConvertToDatetime(Properties.GetPropertyValue("updateDate"));
            }
        }

        public string Url
        {
            get
            {
                var appCache = ApplicationContext.Current.ApplicationCache;
                var r = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

                var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(r.DomainPrefix));

                return findUrlByPrefix ?? Urls.FirstOrDefault();
            }
        }

        public string ContentTypeAlias {
            get
            {
                return Properties.GetPropertyValue("nodeTypeAlias");
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

        public IEnumerable<VariantGroup> VariantGroups {
            get
            {
                return _variantGroupCache.Cache[Store.Alias]
                                        .Where(x => x.Value.ProductKey == Key)
                                        .Select(x => x.Value);
            }
        }

        public IEnumerable<Variant> AllVariants {
            get
            {
                return _variantCache.Cache[Store.Alias]
                    .Where(x => x.Value.ProductKey == Key)
                    .Select(x => x.Value);
            }
        }
        public Dictionary<string, string> Properties = new Dictionary<string, string>();

        /// <summary>
        /// Used by uWebshop extensions
        /// </summary>
        /// <param name="store"></param>
        public Product(Store store)
        {
            _store = store;
        }

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
            _store = store;

            Properties = CreateDefaultUmbracoProperties(node);

            foreach (var prop in node.Properties)
            {
                Properties.Add(prop.Alias, prop.Value?.ToString());
            }

            Urls = UrlService.BuildProductUrls(Slug, Categories, store);

            if (!Urls.Any() || string.IsNullOrEmpty(Title))
            {
                throw new Exception("No url's or no title present in product");
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
