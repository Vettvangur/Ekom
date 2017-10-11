using log4net;
using System;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using Umbraco.Core;
using Umbraco.Web;
using Umbraco.Web.Routing;
using uWebshop.Cache;
using uWebshop.Models;
using uWebshop.Services;
using uWebshop.Utilities;

namespace uWebshop
{
    class CatalogContentFinder : IContentFinder
    {
        ILog _log;
        Configuration _config;
        StoreService _storeSvc;
        IPerStoreCache<Category> _categoryCache;
        IPerStoreCache<Product> _productCache;

        public CatalogContentFinder()
        {
            var container = Configuration.container;

            _config = container.GetService<Configuration>();
            _storeSvc = container.GetService<StoreService>();
            _categoryCache = container.GetService<IPerStoreCache<Category>>();
            _productCache = container.GetService<IPerStoreCache<Product>>();

            var logFac = container.GetService<ILogFactory>();
            _log = logFac.GetLogger(typeof(CatalogContentFinder));
        }

        /// <summary>
        /// Maps virtual URLs to IPublishedContent items
        /// Performs various request related processing
        /// F.x. determining the Store/Currency first from Cookie, then domain and then default
        /// </summary>
        public bool TryFindContent(PublishedContentRequest contentRequest)
        {
            try
            {
                var umbracoContext = contentRequest.RoutingContext.UmbracoContext;
                var httpContext = umbracoContext.HttpContext;
                var umbracoHelper = new UmbracoHelper(umbracoContext);

                // Allows for configuration of content nodes to use for matching all requests
                // Use case: uWebshop populated by adapter, used as in memory cache with no backing umbraco nodes
                var virtualContent = ConfigurationManager.AppSettings["uWebshop.virtualContent"];

                var path = contentRequest.Uri
                                         .AbsolutePath
                                         .ToLower()
                                         .AddTrailing();

                Store store = _storeSvc.GetStore(contentRequest.UmbracoDomain, httpContext);

                if (store == null)
                {
                    throw new Exception("No store found.");
                }

                #region Product and/or Category

                // Requesting Product?
                var product = _productCache.Cache[store.Alias]
                                          .FirstOrDefault(x => x.Value.Urls != null &&
                                                               x.Value.Urls.Contains(path))
                                          .Value;

                int contentId = 0;
                Category category;

                if (product != null)
                {
                    if (virtualContent.InvariantEquals("true"))
                    {
                        contentId = int.Parse(umbracoHelper.GetDictionaryValue("virtualProductNode"));
                    }
                    else
                    {
                        contentId = product.Id;
                    }

                    var urlArray = path.Split('/');
                    var categoryUrlArray = urlArray.Take(urlArray.Count() - 2);
                    var categoryUrl = string.Join("/", categoryUrlArray).AddTrailing();

                    category = _categoryCache.Cache[store.Alias]
                                            .FirstOrDefault(x => x.Value.Urls.Contains(categoryUrl))
                                            .Value;
                }
                else // Request Category?
                {
                    category = _categoryCache.Cache[store.Alias]
                                            .FirstOrDefault(x => x.Value.Urls != null &&
                                                                 x.Value.Urls.Contains(path))
                                            .Value;

                    if (category != null)
                    {
                        if (virtualContent.InvariantEquals("true"))
                        {
                            contentId = int.Parse(umbracoHelper.GetDictionaryValue("virtualCategoryNode"));
                        }
                        else
                        {
                            contentId = category.Id;
                        }
                    }
                    // else Requesting Neither
                }
                #endregion

                var appCache = umbracoContext.Application.ApplicationCache;

                var uwbsRequest = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

                uwbsRequest.Product = product;
                uwbsRequest.Category = category;

                // Request for Product or Category
                if (contentId != 0)
                {
                    var contentCache = umbracoContext.ContentCache;

                    var content = contentCache.GetById(contentId);

                    if (content != null)
                    {
                        contentRequest.PublishedContent = content;

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error("Error trying to find matching content for request", ex);
            }

            return false;
        }
    }
}
