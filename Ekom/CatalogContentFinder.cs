using Ekom.Cache;
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
        ILog _log;
        Configuration _config;
        StoreService _storeSvc;
        IPerStoreCache<Category> _categoryCache;
        IPerStoreCache<Product> _productCache;

        public CatalogContentFinder()
        {
            var container = Configuration.container;

            _config = container.GetInstance<Configuration>();
            _storeSvc = container.GetInstance<StoreService>();
            _categoryCache = container.GetInstance<IPerStoreCache<Category>>();
            _productCache = container.GetInstance<IPerStoreCache<Product>>();

            var logFac = container.GetInstance<ILogFactory>();
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
                var memberShipHelper = new MembershipHelper(umbracoContext);

                // Allows for configuration of content nodes to use for matching all requests
                // Use case: Ekom populated by adapter, used as in memory cache with no backing umbraco nodes
                var virtualContent = ConfigurationManager.AppSettings["Ekom.VirtualContent"];

                var path = contentRequest.Uri
                                         .AbsolutePath
                                         .ToLower()
                                         .AddTrailing();

                if (path == "/")
                {
                    return false;
                }

                Store store = _storeSvc.GetStoreByDomain(contentRequest.UmbracoDomain.DomainName);

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

                if (product != null && !string.IsNullOrEmpty(product.Slug))
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

                    if (category != null && !string.IsNullOrEmpty(category.Slug))
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

                var ekmRequest = appCache.RequestCache.GetCacheItem("ekmRequest") as ContentRequest;

                ekmRequest.Product = product;
                ekmRequest.Category = category;

                // Request for Product or Category
                if (contentId != 0)
                {
                    var contentCache = umbracoContext.ContentCache;

                    var content = contentCache.GetById(contentId);

                    if (content != null)
                    {
                        if (httpContext.User.Identity.IsAuthenticated)
                        {
                            var member = memberShipHelper.GetCurrentMemberProfileModel();

                            var u = new User()
                            {
                                Email = member.Email,
                                Username = member.UserName,
                                UserId = memberShipHelper.GetCurrentMemberId(),
                                Name = member.Name
                            };

                            ekmRequest.User = u;
                        }

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
