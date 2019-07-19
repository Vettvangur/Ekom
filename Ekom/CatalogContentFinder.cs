using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using log4net;
using System;
using System.Configuration;
using System.Linq;
using Umbraco.Core;
using Umbraco.Web;
using Umbraco.Web.Routing;
using Umbraco.Web.Security;

namespace Ekom
{
    class CatalogContentFinder : IContentFinder
    {
        ILogger _logger;
        Configuration _config;
        StoreService _storeSvc;
        IPerStoreCache<ICategory> _categoryCache;
        IPerStoreCache<IProduct> _productCache;

        public CatalogContentFinder()
        {
            var container = Configuration.container;

            _config = container.GetInstance<Configuration>();
            _storeSvc = container.GetInstance<StoreService>();
            _categoryCache = container.GetInstance<IPerStoreCache<ICategory>>();
            _productCache = container.GetInstance<IPerStoreCache<IProduct>>();

            var logFac = container.GetInstance<ILogFactory>();
            _logger = logger;
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
                // Use case: Ekom populated by adapter, used as in memory cache with no backing umbraco nodes
                var virtualContent = ConfigurationManager.AppSettings["Ekom.VirtualContent"];

                var path = contentRequest.Uri
                                         .AbsolutePath
                                         .ToLower()
                                         .AddTrailing();

                if (httpContext.Request.Path.StartsWith("/umbraco"))
                {
                    return false;
                }

                var store = _storeSvc.GetStoreByDomain(contentRequest.UmbracoDomain.DomainName);

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

                var contentId = 0;
                ICategory category;

                if (product != null && !string.IsNullOrEmpty(product.Slug))
                {
                    contentId = virtualContent.InvariantEquals("true") ? int.Parse(umbracoHelper.GetDictionaryValue("virtualProductNode")) : product.Id;

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

                    if (category != null && !string.IsNullOrEmpty(category.Slug))
                    {
                        contentId = virtualContent.InvariantEquals("true") ? int.Parse(umbracoHelper.GetDictionaryValue("virtualCategoryNode")) : category.Id;
                    }
                    // else Requesting Neither
                }
                #endregion

                var appCache = umbracoContext.Application.ApplicationCache;

                if (appCache.RequestCache.GetCacheItem("ekmRequest") is ContentRequest ekmRequest)
                {
                    ekmRequest.Store = store;
                    ekmRequest.Product = product;
                    ekmRequest.Category = category;
                }

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
