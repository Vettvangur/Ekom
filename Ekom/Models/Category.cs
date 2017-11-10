using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using Umbraco.Core.Models;
using Ekom.Cache;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Services;

namespace Ekom.Models
{
    // Here we need to make properties accessible
    public class Category : ICategory
    {
        private IPerStoreCache<Category> _categoryCache
        {
            get
            {
                return Configuration.container.GetInstance<IPerStoreCache<Category>>();
            }
        }

        private IPerStoreCache<Product> _productCache
        {
            get
            {
                return Configuration.container.GetInstance<IPerStoreCache<Product>>();
            }
        }

        public int Id { get; set; }
        public Guid Key { get; set; }
        public string ContentTypeAlias { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Path { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public int ParentCategoryId { get; set; }
        public Store Store { get; set; }
        public int SortOrder { get; set; }
        public IEnumerable<string> Urls { get; set; }
        public int Level { get; set; }
        public string Url
        {
            get
            {
                //var appCache = ApplicationContext.Current.ApplicationCache;
                //var r = appCache.RequestCache.GetCacheItem("ekmRequest") as ContentRequest;

                //var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(r.DomainPrefix));

                var path = HttpContext.Current.Request.Url.AbsolutePath;
                var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(path));

                return findUrlByPrefix ?? Urls.FirstOrDefault();
            }
        }
        public IEnumerable<Category> SubCategories
        {
            get
            {
                return _categoryCache.Cache[Store.Alias]
                                    .Where(x => x.Value.ParentCategoryId == Id)
                                    .Select(x => x.Value)
                                    .OrderBy(x => x.SortOrder);
            }
        }

        [ScriptIgnore]
        [JsonIgnore]
        public IEnumerable<Product> Products
        {
            get
            {
                return _productCache.Cache[Store.Alias]
                                   .Where(x => x.Value.Categories().Any(z => z.Id == Id))
                                   .Select(x => x.Value)
                                   .OrderBy(x => x.SortOrder);
            }
        }

        [ScriptIgnore]
        [JsonIgnore]
        public IEnumerable<Category> SubCategoriesRecursive
        {
            get
            {
                return _categoryCache.Cache[Store.Alias]
                                    .Where(x => x.Value.Level > Level &&
                                                x.Value.Path.Split(',').Contains(Id.ToString()))
                                    .Select(x => x.Value)
                                    .OrderBy(x => x.SortOrder);
            }
        }

        [ScriptIgnore]
        [JsonIgnore]
        public IEnumerable<Product> ProductsRecursive
        {
            get
            {
                return _categoryCache.Cache[Store.Alias]
                                    .Where(x => x.Value.Level >= Level &&
                                                x.Value.Path.Split(',').Contains(Id.ToString()))
                                    .Select(x => x.Value)
                                    .OrderBy(x => x.SortOrder)
                                    .SelectMany(x => x.Products);
            }
        }

        public IEnumerable<Category> Ancestors()
        {
            var list = new List<Category>();

            foreach (var id in Path.Split(','))
            {
                int categoryId;

                if (int.TryParse(id, out categoryId))
                {

                    var category = _categoryCache.Cache[Store.Alias]
                        .FirstOrDefault(x => x.Value.Id == categoryId);

                    if (category.Value != null)
                    {
                        list.Add(category.Value);
                    }

                }
            }

            return list;

        }

        public Category() : base() { }
        public Category(SearchResult item, Store store)
        {
            var pathField = item.Fields["path"];

            var key = item.Fields["key"];

            var _key = new Guid();

            if (!Guid.TryParse(key, out _key))
            {
                throw new Exception("No key present for product.");
            }

            var contentTypeAlias = item.Fields["__NodeTypeAlias"];

            int parentCategoryId = Convert.ToInt32(item.Fields["parentID"]);

            var examineItemsFromPath = NodeHelper.GetAllCatalogItemsFromPath(pathField);

            Id = item.Id;
            Key = _key;
            Path = pathField;
            ParentCategoryId = parentCategoryId;
            Store = store;
            ContentTypeAlias = contentTypeAlias;
            Title = item.GetStoreProperty("title", store.Alias);
            Slug = item.GetStoreProperty("slug", store.Alias);

            SortOrder = Convert.ToInt32(item.Fields["sortOrder"]);
            Level = Convert.ToInt32(item.Fields["level"]);
            CreateDate = ExamineService.ConvertToDatetime(item.Fields["createDate"]);
            UpdateDate = ExamineService.ConvertToDatetime(item.Fields["updateDate"]);

            Urls = UrlService.BuildCategoryUrls(examineItemsFromPath, store);
        }
        public Category(IContent node, Store store)
        {
            var pathField = node.Path;

            int parentCategoryId = node.ParentId;

            var examineItemsFromPath = NodeHelper.GetAllCatalogItemsFromPath(pathField);

            Id = node.Id;
            Key = node.Key;
            Path = pathField;
            ParentCategoryId = parentCategoryId;
            Store = store;

            Title = node.GetStoreProperty("title", store.Alias);
            Slug = node.GetStoreProperty("slug", store.Alias);

            SortOrder = node.SortOrder;
            Level = node.Level;
            CreateDate = node.CreateDate;
            UpdateDate = node.UpdateDate;

            Urls = UrlService.BuildCategoryUrls(examineItemsFromPath, store);
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
