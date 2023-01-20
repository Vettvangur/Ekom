using Ekom.Cache;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Umb
{
    class CatalogContentFinder : IContentFinder
    {
        readonly ILogger<CatalogContentFinder> _logger;
        readonly Configuration _config;
        readonly IStoreService _storeSvc;
        readonly IPerStoreCache<ICategory> _categoryCache;
        readonly IPerStoreCache<IProduct> _productCache;
        readonly AppCaches _appCaches;
        readonly IHttpContextAccessor _httpContextAccessor;
        readonly IUmbracoContextAccessor _umbracoContextAccessor;

        public CatalogContentFinder(
            ILogger<CatalogContentFinder> logger,
            Configuration config,
            IStoreService storeSvc,
            IPerStoreCache<ICategory> categoryCache,
            IPerStoreCache<IProduct> productCache,
            AppCaches appCaches,
            IHttpContextAccessor httpContextAccessor,
            IUmbracoContextAccessor umbracoContextAccessor)
        {
            _logger = logger;
            _config = config;
            _storeSvc = storeSvc;
            _categoryCache = categoryCache;
            _productCache = productCache;
            _appCaches = appCaches;
            _httpContextAccessor = httpContextAccessor;
            _umbracoContextAccessor = umbracoContextAccessor;
        }

        /// <summary>
        /// Maps virtual URLs to IPublishedContent items
        /// Performs various request related processing
        /// F.x. determining the Store/Currency first from Cookie, then domain and then default
        /// </summary>
        public Task<bool> TryFindContent(IPublishedRequestBuilder contentRequest)
        {
            try
            {
                var path = contentRequest.Uri.GetAbsolutePathDecoded().ToLower().AddTrailing();

                if (path.StartsWith("/umbraco"))
                {
                    return Task.FromResult(false); // Not found
                }

                if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
                {
                    return Task.FromResult(false);
                }

                var store = _storeSvc.GetStoreByDomain(contentRequest.Domain?.Name, contentRequest.Culture);

                var virtualContent = _config.VirtualContent;

                #region Product and/or Category

                // Requesting Product?
                var product = _productCache.Cache[store.Alias]
                                .FirstOrDefault(x => x.Value.Urls != null &&
                                                    x.Value.Urls.Contains(path)).Value;

                var contentId = 0;
                ICategory category;

                if (product != null && !string.IsNullOrEmpty(product.GetValue("slug")))
                {
                    //contentId = virtualContent ? int.Parse(umbHelper.GetDictionaryValue("virtualProductNode")) : product.Id;
                    contentId = product.Id;

                    var urlArray = path.Split('/');
                    var categoryUrlArray = urlArray.Take(urlArray.Length - 2);
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

                    if (category != null && !string.IsNullOrEmpty(category.GetValue("slug")))
                    {
                        //contentId = virtualContent.InvariantEquals("true")
                        //    ? int.Parse(umbHelper.GetDictionaryValue("virtualCategoryNode"))
                        //    : category.Id;

                        contentId = category.Id;
                    }
                    // else Requesting Neither
                }
                #endregion

                var httpCtx = _httpContextAccessor.HttpContext;
                var requestCache = _appCaches.RequestCache.Get("ekmRequest", () => new ContentRequest(httpCtx));
                if (requestCache != null && requestCache is ContentRequest ekmRequest)
                {
                    ekmRequest.Store = store;
                    ekmRequest.Product = product;
                    ekmRequest.Category = category;
                }

                // Request for Product or Category
                if (contentId != 0)
                {
                    var content = umbracoContext.Content.GetById(contentId);

                    if (content != null)
                    {
                        contentRequest.SetPublishedContent(content);
                        return Task.FromResult(true);
                    }
                    else
                    {
                        return Task.FromResult(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find Ekom content.");
            }

            return Task.FromResult(false);
        }
    }
}
