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
            return null;
        }

        /// <summary>
        /// Our <see cref="CatalogContentFinder"/> takes care of routing, 
        /// this is mostly used to display URLs in the backoffice.
        /// </summary>
        public IEnumerable<UrlInfo> GetOtherUrls(UmbracoContext umbracoContext, int id, Uri current)
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

                    var distinctUrls = list.Distinct(new UrlInfoComparer());

                    return distinctUrls;
                }
                catch (Exception ex)
                {
                    _logger.Error<CatalogUrlProvider>(ex, "EkomUrlProvider-GetOtherUrls Failed.");
                }

                return null;
            });
        }

        class UrlInfoComparer : EqualityComparer<UrlInfo>
        {
            public override bool Equals(UrlInfo b1, UrlInfo b2)
            {
                if (b1 == null && b2 == null)
                    return true;
                else if (b1 == null || b2 == null)
                    return false;

                return string.Equals(
                    b1.Text + b1.Culture,
                    b2.Text + b2.Culture,
                    StringComparison.InvariantCultureIgnoreCase);
            }

            public override int GetHashCode(UrlInfo bx)
            {
                string hCode = bx.Text + bx.Culture + bx.IsUrl;
                return hCode.GetHashCode();
            }
        }
    }
}
