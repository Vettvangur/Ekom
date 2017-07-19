using log4net;
using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Cache;
using uWebshop.App_Start;
using uWebshop.Cache;
using uWebshop.Interfaces;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.API
{
    /// <summary>
    /// The uWebshop API, grants access to the current product/category/variant 
    /// and various other depending on your current routed context.
    /// </summary>
    public class Catalog
    {
        static Catalog Instance { get; } = new Catalog();

        ILog _log;
        ApplicationContext _appCtx;
        ICacheProvider _reqCache => _appCtx.ApplicationCache.RequestCache;

        IPerStoreCache<Product> _productCache;
        IPerStoreCache<Category> _categoryCache;
        IPerStoreCache<Variant> _variantCache;
        StoreService _storeSvc;

        /// <summary>
        /// ctor
        /// </summary>
        public Catalog()
        {
            var container = UnityConfig.GetConfiguredContainer();

            _appCtx = container.Resolve<ApplicationContext>();
            _productCache = container.Resolve<IPerStoreCache<Product>>();
            _categoryCache = container.Resolve<IPerStoreCache<Category>>();
            _variantCache = container.Resolve<IPerStoreCache<Variant>>();
            _storeSvc = container.Resolve<StoreService>();

            var logFac = container.Resolve<ILogFactory>();
            _log = logFac.GetLogger(typeof(Catalog));
        }

        public Product GetProduct()
        {
            var r = _reqCache.GetCacheItem("uwbsRequest") as ContentRequest;

            return r?.Product;
        }

        public Product GetProduct(Guid Id)
        {
            var r = _reqCache.GetCacheItem("uwbsRequest") as ContentRequest;

            if (r?.Store != null)
            {
                var product = GetProduct(r.Store.Alias, Id);

                return product;
            }

            return null;
        }

        public Product GetProduct(string storeAlias, Guid Id)
        {
            var product = _productCache.Cache[storeAlias].FirstOrDefault(x => x.Value.Key == Id).Value;

            return product;
        }

        public Product GetProduct(int Id)
        {
            var r = _reqCache.GetCacheItem("uwbsRequest") as ContentRequest;

            if (r != null && r.Store != null)
            {
                var product = GetProduct(r.Store.Alias, Id);

                return product;
            }

            return null;
        }

        public Product GetProduct(string storeAlias, int Id)
        {
            var product = _productCache.Cache[storeAlias].FirstOrDefault(x => x.Value.Id == Id).Value;

            return product;
        }
        public IEnumerable<Product> GetAllProducts()
        {
            var r = _reqCache.GetCacheItem("uwbsRequest") as ContentRequest;

            if (r != null && r.Store != null)
            {
                var products = GetAllProducts(r.Store.Alias);

                return products;
            }

            return null;
        }

        public IEnumerable<Product> GetAllProducts(string storeAlias)
        {
            return _productCache.Cache[storeAlias].Select(x => x.Value).OrderBy(x => x.SortOrder);
        }

        public Category GetCategory()
        {
            var r = _reqCache.GetCacheItem("uwbsRequest") as ContentRequest;

            if (r != null && r.Category != null)
            {
                return r.Category;
            }

            return null;
        }

        public Category GetCategory(int Id)
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var category = GetCategory(store.Alias, Id);

                return category;
            }

            return null;
        }

        public Category GetCategory(string storeAlias, int Id)
        {
            return _categoryCache.Cache[storeAlias].FirstOrDefault(x => x.Value.Id == Id).Value;
        }

        public IEnumerable<Category> GetRootCategories()
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var categories = GetRootCategories(store.Alias);

                return categories;
            }

            return null;
        }

        public IEnumerable<Category> GetRootCategories(string storeAlias)
        {
            return _categoryCache.Cache[storeAlias]
                                .Where(x => x.Value.Level == 3)
                                .Select(x => x.Value)
                                .OrderBy(x => x.SortOrder);
        }

        public Variant GetVariant(Guid Id)
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var variant = GetVariant(store.Alias, Id);

                return variant;
            }

            return null;
        }
        public Variant GetVariant(string storeAlias, Guid Id)
        {
            var variant = _variantCache.Cache[storeAlias]
                                      .FirstOrDefault(x => x.Value.Key == Id)
                                      .Value;

            return variant;
        }
    }
}
