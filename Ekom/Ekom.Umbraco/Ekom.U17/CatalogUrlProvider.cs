using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Umb;

internal sealed class CatalogUrlProvider : IUrlProvider
{
    private const string CacheKey = "EkomUrlProvider-GetOtherUrls-";
    private const string ProviderAlias = "Ekom.CatalogUrlProvider";

    public string Alias => ProviderAlias;

    private readonly ILogger<CatalogUrlProvider> _logger;
    private readonly IAppCache _requestCache;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public CatalogUrlProvider(
        ILogger<CatalogUrlProvider> logger,
        AppCaches appCaches,
        IUmbracoContextAccessor umbracoContextAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        IConfiguration configuration,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _requestCache = appCaches.RequestCache;
        _umbracoContextAccessor = umbracoContextAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _configuration = configuration;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public UrlInfo? GetUrl(IPublishedContent content, UrlMode mode, string? culture, Uri current)
    {
        if (content is null)
        {
            return null;
        }

        if (!content.IsDocumentType("ekmProduct") && !content.IsDocumentType("ekmCategory"))
        {
            return null;
        }

        try
        {
            var urls = GetUrls(content.Id, current);
            return urls?.FirstOrDefault(x => x.Culture == culture) ?? urls?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Ekom URL for content {ContentId}", content.Id);
            return null;
        }
    }

    public IEnumerable<UrlInfo> GetOtherUrls(int id, Uri current)
    {
        return GetUrls(id, current) ?? Enumerable.Empty<UrlInfo>();
    }

    public Task<UrlInfo?> GetPreviewUrlAsync(IContent content, string? culture, string? segment)
    {
        return Task.FromResult<UrlInfo?>(null);
    }

    private IEnumerable<UrlInfo>? GetUrls(int id, Uri current)
    {
        return _requestCache.GetCacheItem(CacheKey + id, () =>
        {
            if (!_umbracoContextAccessor.TryGetUmbracoContext(out var context))
            {
                using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();
                return GetUrls(id, current, contextReference.UmbracoContext);
            }

            return GetUrls(id, current, context);
        });
    }

    private IEnumerable<UrlInfo> GetUrls(int id, Uri current, IUmbracoContext context)
    {
        var content = context.Content?.GetById(id);

        if (content == null || (!content.IsDocumentType("ekmProduct") && !content.IsDocumentType("ekmCategory")))
        {
            return Enumerable.Empty<UrlInfo>();
        }

        if (content.ContentType.Alias == "ekmCategory" && content.Value<bool>("ekmVirtualUrl"))
        {
            return Enumerable.Empty<UrlInfo>();
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var storeApi = scope.ServiceProvider.GetRequiredService<API.Store>();
        var catalogApi = scope.ServiceProvider.GetRequiredService<API.Catalog>();

        var stores = storeApi.GetAllStores().ToList();
        if (!stores.Any())
        {
            return Enumerable.Empty<UrlInfo>();
        }

        var absoluteUrls = _configuration["Ekom:AbsoluteUrls"].IsBoolean();
        var urls = new HashSet<UrlInfo>();

        foreach (var store in stores)
        {
            try
            {
                INodeEntityWithUrl? node = content.ContentType.Alias == "ekmProduct"
                    ? catalogApi.GetProduct(content.Key, store.Alias, raiseEvent: false)
                    : catalogApi.GetCategory(content.Key, store.Alias, raiseEvent: false);

                if (node != null)
                {
                    PopulateUrls(node, store, urls, current, absoluteUrls);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{CacheKey} Failed.", CacheKey);
                return Enumerable.Empty<UrlInfo>();
            }
        }

        return urls;
    }

    private static void PopulateUrls(INodeEntityWithUrl node, IStore store, HashSet<UrlInfo> urls, Uri current, bool absoluteUrls)
    {
        var slugValue = JsonConvert.DeserializeObject<PropertyValue>(node.GetRawValue("slug"));
        var storeDomains = store.Domains.ToList();
        var distinctDomains = absoluteUrls ? storeDomains : storeDomains.DistinctBy(x => DomainHelper.GetDomainPrefix(x.DomainName));

        if (slugValue?.Type == PropertyEditorType.Language)
        {
            foreach (var domain in distinctDomains)
            {
                var url = node.UrlsWithContext.FirstOrDefault(x =>
                    x.Culture == domain.LanguageIsoCode && x.Domain == domain.DomainName)?.Url;

                if (!string.IsNullOrEmpty(url))
                {
                    urls.Add(CreateUrlInfo(UrlModifier(url, absoluteUrls, current, domain), domain.LanguageIsoCode));
                }
            }

            return;
        }

        foreach (var url in node.Urls)
        {
            foreach (var domain in storeDomains)
            {
                urls.Add(CreateUrlInfo(UrlModifier(url, absoluteUrls, current, domain), store.Title));
            }
        }
    }

    private static UrlInfo CreateUrlInfo(string url, string culture)
    {
        var uri = new Uri(url, UriKind.RelativeOrAbsolute);
        return new UrlInfo(uri, culture, null, ProviderAlias, uri.IsAbsoluteUri);
    }

    private static string UrlModifier(string url, bool absoluteUrls, Uri current, Ekom.Models.UmbracoDomain domain)
    {
        if (absoluteUrls && !domain.DomainName.StartsWith('/'))
        {
            var domainName = domain.DomainName;
            var slashIndex = domainName.IndexOf('/');
            if (slashIndex != -1)
            {
                domainName = domainName[..slashIndex];
            }

            return current.Scheme + "://" + domainName + url;
        }

        return url.StartsWith('/') ? url : "/" + url;
    }
}
