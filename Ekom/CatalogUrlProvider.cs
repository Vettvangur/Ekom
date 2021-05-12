using Ekom.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Ekom
{
    class CatalogUrlProvider : IUrlProvider
    {
        readonly ILogger _logger;
        readonly IAppCache _reqCache;

        public CatalogUrlProvider(ILogger logger, AppCaches appCaches)
        {
            _logger = logger;
            _reqCache = appCaches.RequestCache;
        }

        public UrlInfo GetUrl(
            UmbracoContext umbracoContext,
            IPublishedContent content,
            UrlMode mode,
            string culture,
            Uri current)
        {
            try
            {
                var urls = GetUrls(umbracoContext, content.Id, current);

                // In practice this will simply return the first url from the collection
                // since we're comparing store title to culture.
                return urls.FirstOrDefault(x => x.Culture == culture) ?? urls.FirstOrDefault();
            }
#pragma warning disable CA1031 // This must not fail, otherwise Umbraco fails
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.Error<CatalogUrlProvider>(ex);
                return null;
            }
        }

        /// <summary>
        /// Our <see cref="CatalogContentFinder"/> takes care of routing, 
        /// this is mostly used to display URLs in the backoffice.
        /// </summary>
        public IEnumerable<UrlInfo> GetOtherUrls(UmbracoContext umbracoContext, int id, Uri current)
        {
            return GetUrls(umbracoContext, id, current);
        }

        private IEnumerable<UrlInfo> GetUrls(UmbracoContext umbracoContext, int id, Uri current)
        {
            return _reqCache.GetCacheItem(
                "EkomUrlProvider-GetOtherUrls-" + id,
                () =>
                {
                    try
                    {
                        var content = umbracoContext.Content.GetById(id);

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

                        // UrlInfo implements IEquatable
                        var distinctUrls = list.Distinct();

                        return distinctUrls;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error<CatalogUrlProvider>(ex, "EkomUrlProvider-GetOtherUrls Failed.");
                    }

                    return null;
                });
        }
    }
}
