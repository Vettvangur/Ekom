using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
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
                return Convert.ToInt32(GetPropertyValue<string>("id"));
            }
        }
        [JsonIgnore]
        public Guid Key {
            get
            {
                var key = GetPropertyValue<string>("key");

                var _key = new Guid();

                if (!Guid.TryParse(key, out _key))
                {
                    throw new Exception("No key present for product.");
                }

                return _key;
            }
        }
        [JsonIgnore]
        public string Title {
            get
            {
                return Properties.GetStoreProperty("title", _store.Alias);
            }
        }
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
        [JsonIgnore]
        public List<ICategory> Categories {
            get
            {
                int categoryId = Convert.ToInt32(GetPropertyValue<string>("parentID"));

                var categoryField = Properties.Any(x => x.Key == "categories") ?
                                    GetPropertyValue<string>("categories") : "";

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
                return Convert.ToInt32(GetPropertyValue<string>("sortOrder"));
            }
        }
        [JsonIgnore]
        public int Level {
            get
            {
                return Convert.ToInt32(GetPropertyValue<string>("level"));
            }
        }
        [JsonIgnore]
        public string Path
        {
            get
            {
                return GetPropertyValue<string>("path");
            }
        }
        [JsonIgnore]
        public DateTime CreateDate {
            get
            {
                return ExamineHelper.ConvertToDatetime(GetPropertyValue<string>("createDate"));
            }
        }
        [JsonIgnore]
        public DateTime UpdateDate {
            get
            {
                return ExamineHelper.ConvertToDatetime(GetPropertyValue<string>("updateDate"));
            }
        }
        public string Url {
            get {

                var appCache = ApplicationContext.Current.ApplicationCache;
                var r = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

                var defaultUrl = Urls.FirstOrDefault();
                var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(r.DomainPrefix));

                if (findUrlByPrefix != null) {
                    return findUrlByPrefix;
                }

                return defaultUrl;
            }
        }
        [JsonIgnore]
        public string ContentTypeAlias {
            get
            {
                return GetPropertyValue<string>("nodeTypeAlias");
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
        public List<UmbracoProperty> Properties = new List<UmbracoProperty>();

        public T GetPropertyValue<T>(string propertyAlias)
        {
            propertyAlias = propertyAlias.ToLowerInvariant();

            if (!string.IsNullOrEmpty(propertyAlias))
            {
                if (Properties.Any(x => x.Key.ToLowerInvariant() == propertyAlias))
                {
                    var property = Properties.FirstOrDefault(x => x.Key.ToLowerInvariant() == propertyAlias);

                    return property == null ? default(T) : (T)property.Value;
                }

            }

            return default(T);
        }

        public Product(): base() { }
        public Product(SearchResult item, Store store)
        {
            _store = store;

            foreach (var field in item.Fields.Where(x => !x.Key.Contains("__")))
            {
                Properties.Add(new UmbracoProperty
                {
                    Key = field.Key,
                    Value = field.Value
                });
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

                Properties.AddRange(CreateDefaultUmbracoProperties(node));

                foreach (var prop in node.Properties)
                {
                    Properties.Add(new UmbracoProperty
                    {
                        Key = prop.Alias,
                        Value = prop.Value
                    });
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

        public static List<UmbracoProperty> CreateDefaultUmbracoProperties(IContent node)
        {
            var properties = new List<UmbracoProperty>();

            properties.Add(new UmbracoProperty {
                Key = "id",
                Value = node.Id.ToString()
            });
            properties.Add(new UmbracoProperty
            {
                Key = "key",
                Value = node.Key.ToString()
            });
            properties.Add(new UmbracoProperty
            {
                Key = "path",
                Value = node.Path
            });
            properties.Add(new UmbracoProperty
            {
                Key = "level",
                Value = node.Level.ToString()
            });
            properties.Add(new UmbracoProperty
            {
                Key = "sortOrder",
                Value = node.SortOrder.ToString()
            });
            properties.Add(new UmbracoProperty
            {
                Key = "parentID",
                Value = node.ParentId.ToString()
            });
            properties.Add(new UmbracoProperty
            {
                Key = "writerID",
                Value = node.WriterId.ToString()
            });
            properties.Add(new UmbracoProperty
            {
                Key = "creatorID",
                Value = node.CreatorId.ToString()
            });
            properties.Add(new UmbracoProperty
            {
                Key = "nodeTypeAlias",
                Value = node.ContentType.Alias
            });
            properties.Add(new UmbracoProperty
            {
                Key = "updateDate",
                Value = node.UpdateDate.ToString("yyyyMMddHHmmssfff")
            });
            properties.Add(new UmbracoProperty
            {
                Key = "createDate",
                Value = node.CreateDate.ToString("yyyyMMddHHmmssfff")
            });

            return properties;
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
