using Ekom.Models;
using Ekom.Utilities;
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

        public CatalogUrlProvider(ILogger<CatalogUrlProvider> logger, AppCaches appCaches, IUmbracoContextAccessor umbracoContextAccessor)
        {
            _logger = logger;
            _reqCache = appCaches.RequestCache;
            _umbracoContextAccessor = umbracoContextAccessor;
        }

        public UrlInfo GetUrl(
            IPublishedContent content,
            UrlMode mode,
            string culture,
            Uri current)
        {
            try
            {
                
                var urls = GetUrls(content.Id, current);

                // In practice this will simply return the first url from the collection
                // since we're comparing store title to culture.
                return urls.FirstOrDefault(x => x.Culture == culture) ?? urls.FirstOrDefault();
            }
#pragma warning disable CA1031 // This must not fail, otherwise Umbraco fails
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.LogError(ex.ToString());
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
#pragma warning disable CS8603 // Possible null reference return.
            return _reqCache.GetCacheItem(
                "EkomUrlProvider-GetOtherUrls-" + id,
                () =>
                {
                    try
                    {
                        _umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? context);
                        var content = context.Content.GetById(id);

                        if (content == null ||
                            (content.ContentType.Alias != "ekmProduct" && content.ContentType.Alias != "ekmCategory"))
                            return Enumerable.Empty<UrlInfo>();

                        var list = new List<UrlInfo>();

                        var stores = API.Store.Instance.GetAllStores().ToList();

                        if (!stores.Any()) return list;

                        foreach (var store in stores)
                        {
                            INodeEntityWithUrl node;
                            if (content.ContentType.Alias == "ekmProduct")
                            {
                                node = API.Catalog.Instance.GetProduct(store.Alias, id);
                            }
                            else
                            {
                                node = API.Catalog.Instance.GetCategory(store.Alias, id);
                            }

                            if (node != null)
                            {
                                var slugValue = JsonConvert.DeserializeObject<PropertyValue>(node.GetValue("slug"));

                                if (slugValue.Type == Utilities.PropertyEditorType.Language)
                                {
                                    foreach (var domain in store.Domains.DistinctBy(x => DomainHelper.GetDomainPrefix(x.DomainName)).Select((value, i) => new { i, value }))
                                    {
                                       
                                        var url = node.Urls.ToArray()[domain.i];
                                        
                                        list.Add(new UrlInfo(
                                            url,
                                            true,
                                            domain.value.LanguageIsoCode)
                                        );
                                    }
                                } else
                                {
                                    foreach (var url in node.Urls)
                                    {
                                        list.Add(new UrlInfo(
                                            url,
                                            true,
                                            store.Title)
                                        );
                                    }
                                }


                            }
                        }

                        // UrlInfo implements IEquatable
                        var distinctUrls = list.Distinct();

                        return distinctUrls;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "EkomUrlProvider-GetOtherUrls Failed.");
                    }

                    return null;
                });
#pragma warning restore CS8603 // Possible null reference return.
        }
    }
}
