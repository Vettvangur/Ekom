using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Models;
using uWebshop.Cache;
using uWebshop.Helpers;
using uWebshop.Services;

namespace uWebshop.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Path { get; set; }
        public decimal OriginalPrice { get; set; }
        public int Stock { get; set; }
        public List<Category> Categories { get; set; }
        public Store Store { get; set; }
        public int SortOrder { get; set; }
        public int Level { get; set; }
        public string Url {
            get {

                var appCache = ApplicationContext.Current.ApplicationCache;
                var r = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

                var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(r.DomainPrefix));

                return findUrlByPrefix ?? Urls.FirstOrDefault();
            }
        }
        public IEnumerable<string> Urls { get; set; }
        public Price Price
        {
            get
            {
                return new Price(OriginalPrice);
            }
        }
        public IEnumerable<VariantGroup> VariantGroups {
            get
            {
                return VariantGroupCache.Cache[Store.Alias]
                                        .Where(x => x.Value.ProductId == Id)
                                        .Select(x => x.Value);
            }
        }
        public IEnumerable<Variant> AllVariants {
            get
            {
                return VariantCache.Cache[Store.Alias]
                                   .Where(x => x.Value.ProductId == Id)
                                   .Select(x => x.Value);
            }
        }

        public Product(): base() { }
        public Product(SearchResult item, Store store)
        {
            var pathField = item.Fields["path"];

            int categoryId = Convert.ToInt32(item.Fields["parentID"]);

            var categoryField = item.Fields.Any(x => x.Key == "categories") ? 
                                item.Fields["categories"] : "";

            var categories = new List<Category>();

            var primaryCategory = CategoryCache.Cache[store.Alias]
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
                        = CategoryCache.Cache[store.Alias]
                                       .FirstOrDefault(x => x.Value.Id == intCatId)
                                       .Value;

                    if (categoryItem != null && !categories.Contains(categoryItem))
                    {
                        categories.Add(categoryItem);
                    }
                }
            }

            var priceField = item.GetStoreProperty("price", store.Alias);

            decimal originalPrice = 0;
            decimal.TryParse(priceField, out originalPrice);

            Id            = item.Id;
            Path          = pathField;
            OriginalPrice = originalPrice;
            Categories    = categories;
            Store         = store;

            Title         = item.GetStoreProperty("title", store.Alias);
            Slug          = item.GetStoreProperty("slug", store.Alias);

            SortOrder     = Convert.ToInt32(item.Fields["sortOrder"]);
            Level         = Convert.ToInt32(item.Fields["level"]);

            Urls          = UrlService.BuildProductUrls(Slug, Categories, store);

            if (!Urls.Any() || string.IsNullOrEmpty(Title))
            {
                throw new Exception("No url's or no title present");
            }
        }
        public Product(IContent node, Store store)
        {
            var pathField = node.Path;

            int categoryId = Convert.ToInt32(node.ParentId);

            var categoryProperty = node.GetValue<string>("categories");

            var categories = new List<Category>();

            var primaryCategory = CategoryCache.Cache[store.Alias]
                                                .FirstOrDefault(x => x.Value.Id == categoryId)
                                                .Value;

            if (primaryCategory != null)
            {
                categories.Add(primaryCategory);
            }

            if (!string.IsNullOrEmpty(categoryProperty))
            {
                var categoryIds = categoryProperty.Split(',');

                foreach (var catId in categoryIds)
                {
                    var intCatId = Convert.ToInt32(catId);

                    var categoryItem
                        = CategoryCache.Cache[store.Alias]
                                        .FirstOrDefault(x => x.Value.Id == intCatId)
                                        .Value;

                    if (categoryItem != null && !categories.Contains(categoryItem))
                    {
                        categories.Add(categoryItem);
                    }
                }
            }

            var priceField = node.GetStoreProperty("price", store.Alias);

            decimal originalPrice = 0;
            decimal.TryParse(priceField, out originalPrice);

            Id = node.Id;
            Path = pathField;
            OriginalPrice = originalPrice;
            Categories = categories;
            Store = store;

            Title = node.GetStoreProperty("title", store.Alias);
            Slug = node.GetStoreProperty("slug", store.Alias);

            SortOrder = node.SortOrder;
            Level = node.Level;

            Urls = UrlService.BuildProductUrls(Slug, Categories, store);

            if (!Urls.Any() || string.IsNullOrEmpty(Title))
            {
                throw new Exception("No url's or no title present");
            }
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
