using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
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

        public CatalogUrlProvider(ILogger<CatalogUrlProvider> logger, AppCaches appCaches, IUmbracoContextAccessor umbracoContextAccessor)
        {
            _logger = logger;
            _reqCache = appCaches.RequestCache;
            _umbracoContextAccessor = umbracoContextAccessor;
        }

        public UrlInfo GetUrl(IPublishedContent content, UrlMode mode, string culture, Uri current)
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
                return null;
            }
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

                        var stores = API.Store.Instance.GetAllStores().ToList();
                        
                        if (!stores.Any())
                        {
                            return Enumerable.Empty<UrlInfo>();
                        }

                        var urls = new HashSet<UrlInfo>();
                        foreach (var store in stores)
                        {
                            try
                            {
                                INodeEntityWithUrl node = content.ContentType.Alias == "ekmProduct"
                                    ? API.Catalog.Instance.GetProduct(store.Alias, id)
                                    : API.Catalog.Instance.GetCategory(store.Alias, id);

                                if (node != null)
                                {
                                    PopulateUrls(node, store, urls);
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

        private void PopulateUrls(INodeEntityWithUrl node, IStore store, HashSet<UrlInfo> urls)
        {
            var slugValue = JsonConvert.DeserializeObject<PropertyValue>(node.GetValue("slug"));
            
            if (slugValue?.Type == PropertyEditorType.Language)
            {
                var distinctDomains = store.Domains.DistinctBy(x => DomainHelper.GetDomainPrefix(x.DomainName));
                
                foreach (var domain in distinctDomains.Select((value, i) => new { value, i }))
                {

                    var url = node.Urls.ElementAtOrDefault(domain.i);
                    if (url != null)
                    {
                        urls.Add(new UrlInfo(url, true, domain.value.LanguageIsoCode));
                    }
                }
            }
            else
            {
                foreach (var url in node.Urls)
                {
                    urls.Add(new UrlInfo(url, true, store.Title));
                }
            }
        }
    }
}

