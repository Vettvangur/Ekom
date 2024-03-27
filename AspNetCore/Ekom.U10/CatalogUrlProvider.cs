using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Umb
{
    class CatalogUrlProvider : IUrlProvider
    {
        readonly ILogger<CatalogUrlProvider> _logger;
        readonly IAppCache _reqCache;
        readonly IUmbracoContextAccessor _umbracoContextAccessor; 
        readonly IConfiguration _configuration;

        public CatalogUrlProvider(ILogger<CatalogUrlProvider> logger, AppCaches appCaches, IUmbracoContextAccessor umbracoContextAccessor, IConfiguration configuration)
        {
            _logger = logger;
            _reqCache = appCaches.RequestCache;
            _umbracoContextAccessor = umbracoContextAccessor;
            _configuration = configuration;
        }

        public UrlInfo? GetUrl(IPublishedContent content, UrlMode mode, string culture, Uri current)
        {
            if (content is null)
            {
                return null;
            }

            if (content.IsDocumentType("ekmProduct") || content.IsDocumentType("ekmCategory"))
            {
                try
                {
                    var urls = GetUrls(content.Id, current);
                    // In practice this will simply return the first url from the collection
                    // since we're comparing store title to culture.
                    return urls.FirstOrDefault(x => x.Culture == culture) ?? urls.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get url");
                }
               
            }

            return null;
        }

        /// <summary>
        /// Our <see cref="CatalogContentFinder"/> takes care of routing, 
        /// this is mostly used to display URLs in the backoffice.
        /// </summary>
        public IEnumerable<UrlInfo> GetOtherUrls(int id, Uri current)
        {
            return GetUrls(id, current);
        }

        private IEnumerable<UrlInfo> GetUrls(int id, Uri current)
        {
            const string cacheKey = "EkomUrlProvider-GetOtherUrls-";

#pragma warning disable CS8603 // Possible null reference return.
            return _reqCache.GetCacheItem(
                    "EkomUrlProvider-GetOtherUrls-" + id,
                    () =>
                    {
                        if (!_umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? context))
                        {
                            return Enumerable.Empty<UrlInfo>();
                        }

                        var content = context?.Content?.GetById(id);
                        
                        if (content == null ||
                            (content.ContentType.Alias != "ekmProduct" && content.ContentType.Alias != "ekmCategory"))
                        {
                            return Enumerable.Empty<UrlInfo>();
                        }

                        if (content.ContentType.Alias == "ekmCategory" && content.Value<bool>("ekmVirtualUrl")) {
                            return Enumerable.Empty<UrlInfo>();
                        }

                        var stores = API.Store.Instance.GetAllStores().ToList();
                        
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
                                INodeEntityWithUrl node = content.ContentType.Alias == "ekmProduct"
                                    ? API.Catalog.Instance.GetProduct(id, store.Alias)
                                    : API.Catalog.Instance.GetCategory(id, store.Alias);

                                if (node != null)
                                {
                                    PopulateUrls(node, store, urls, current, absoluteUrls);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"{cacheKey} Failed.");
                                return Enumerable.Empty<UrlInfo>();
                            }
                        }

                        return urls;
                    });
#pragma warning restore CS8603 // Possible null reference return.


        }

        private void PopulateUrls(INodeEntityWithUrl node, IStore store, HashSet<UrlInfo> urls, Uri current, bool absoluteUrls)
        {
            var slugValue = JsonConvert.DeserializeObject<PropertyValue>(node.GetValue("slug"));

            var distinctDomains = absoluteUrls ? store.Domains : store.Domains.DistinctBy(x => DomainHelper.GetDomainPrefix(x.DomainName));
            
            if (slugValue?.Type == PropertyEditorType.Language)
            {

                foreach (var domain in distinctDomains.Select((value, i) => new { value, i }))
                {

                    var url = node.UrlsWithContext.FirstOrDefault(x =>
                        x.Culture == domain.value.LanguageIsoCode && x.Domain == domain.value.DomainName)?.Url;
                    
                    if (!string.IsNullOrEmpty(url))
                    {
                        url = UrlModifier(url, absoluteUrls, current, domain.value);
                        
                        urls.Add(new UrlInfo(url, true, domain.value.LanguageIsoCode));
                    }
                }
            }
            else
            {
                foreach (var url in node.Urls)
                {
                    foreach (var domain in store.Domains)
                    {
                        urls.Add(new UrlInfo(UrlModifier(url, absoluteUrls, current, domain), true, store.Title));
                    }

                }
            }
        }

        private string UrlModifier(string url, bool absoluteUrls, Uri current, UmbracoDomain domain)
        {
            if (absoluteUrls && !domain.DomainName.StartsWith("/"))
            {
                var domainName = domain.DomainName;

                int slashIndex = domainName.IndexOf('/');
                if (slashIndex != -1)
                {
                    domainName = domainName.Substring(0, slashIndex);
                }

                return current.Scheme + "://" + domainName + url;
            }

            return !url.StartsWith("/") ? "/" + url : url;
        }
    }
}

