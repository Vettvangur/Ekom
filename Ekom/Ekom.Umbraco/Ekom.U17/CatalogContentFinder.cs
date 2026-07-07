using Ekom.Cache;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Umb;

internal sealed class CatalogContentFinder : IContentFinder
{
    private readonly ILogger<CatalogContentFinder> _logger;
    private readonly IStoreService _storeService;
    private readonly AppCaches _appCaches;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public CatalogContentFinder(
        ILogger<CatalogContentFinder> logger,
        Configuration config,
        IStoreService storeService,
        IPerStoreIndexedCache<ICategory> categoryCache,
        IPerStoreIndexedCache<IProduct> productCache,
        AppCaches appCaches,
        IHttpContextAccessor httpContextAccessor,
        IUmbracoContextAccessor umbracoContextAccessor,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _storeService = storeService;
        _appCaches = appCaches;
        _httpContextAccessor = httpContextAccessor;
        _umbracoContextAccessor = umbracoContextAccessor;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Task<bool> TryFindContent(IPublishedRequestBuilder contentRequest)
    {
        try
        {
            var path = contentRequest.Uri.GetAbsolutePathDecoded().ToLowerInvariant().AddTrailing();

            if (path.StartsWith("/umbraco", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(false);
            }

            if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
            {
                return Task.FromResult(false);
            }

            var store = GetStore(contentRequest);
            using var scope = _serviceScopeFactory.CreateScope();
            var catalogApi = scope.ServiceProvider.GetRequiredService<API.Catalog>();

            var product = catalogApi.GetProductByRoute(path, store?.Alias);
            var contentId = 0;
            ICategory? category;

            if (product != null && !string.IsNullOrEmpty(product.GetValue("slug", fallback: true)))
            {
                contentId = product.Id;
                var urlArray = path.Split('/');
                var categoryUrlArray = urlArray.Take(urlArray.Length - 2);
                var categoryUrl = string.Join("/", categoryUrlArray).AddTrailing();

                category = catalogApi.GetCategoryByRoute(categoryUrl, store?.Alias);
            }
            else
            {
                category = catalogApi.GetCategoryByRoute(path, store?.Alias);

                if (category != null && !string.IsNullOrEmpty(category.GetValue("slug", fallback: true)))
                {
                    contentId = category.Id;
                }
            }

            if (_appCaches.RequestCache.Get("ekmRequest", () => new ContentRequest()) is ContentRequest ekmRequest)
            {
                ekmRequest.Product = product;
                ekmRequest.Category = category;
            }

            if (contentId == 0)
            {
                return Task.FromResult(false);
            }

            var content = umbracoContext.Content?.GetById(contentId);

            if (content != null && !content.Value<bool>("ekmVirtualUrl"))
            {
                contentRequest.SetPublishedContent(content);
                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find Ekom content.");
        }

        return Task.FromResult(false);
    }

    private IStore? GetStore(IPublishedRequestBuilder contentRequest)
    {
        if (_httpContextAccessor.HttpContext?.Items[Configuration.EkmRequestKey] is Lazy<object> lazyRequest &&
            lazyRequest.Value is ContentRequest request &&
            request.Store != null)
        {
            return request.Store;
        }

        return _storeService.GetStoreByDomain(contentRequest.Domain?.Name, contentRequest.Culture);
    }
}
