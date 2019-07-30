using Ekom.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Ekom
{
    class CatalogUrlProvider : IUrlProvider
    {
        ILogger _logger;
        IAppCache _reqCache;

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
            return _reqCache.GetCacheItem(
                "EkomUrlProvider-GetUrl-" + content.Id,
                () =>
                {
                    try
                    {
                        if (content == null ||
                            (content.ContentType.Alias != "ekmProduct" && content.ContentType.Alias != "ekmCategory")) return null;

                        var stores = API.Store.Instance.GetAllStores();

                        if (!stores.Any()) return null;

                        if (content.ContentType.Alias == "ekmProduct")
                        {
                            var product = API.Catalog.Instance.GetProduct(stores.First().Alias, content.Id);

                            if (product != null)
                            {
                                return new UrlInfo(product.Url, true, culture);
                            }

                        }
                        else
                        {
                            var category = API.Catalog.Instance.GetCategory(stores.First().Alias, content.Id);

                            if (category != null)
                            {
                                return new UrlInfo(category.Url, true, culture);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error<CatalogUrlProvider>(ex, "EkomUrlProvider Get Url Failed");
                    }

                    return null;
                });
        }

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

                    if (stores.Count() <= 1) return list;

                    foreach (var store in stores.Skip(1))
                    {
                        if (content.ContentType.Alias == "ekmProduct")
                        {
                            var product = API.Catalog.Instance.GetProduct(store.Alias, id);

                            if (product != null)
                            {
                                list.Add(new UrlInfo(
                                    product.Url,
                                    true,
                                    store.Alias)
                                );
                            }
                        }
                        else
                        {
                            var category = API.Catalog.Instance.GetCategory(store.Alias, id);

                            if (category != null)
                            {
                                list.Add(new UrlInfo(
                                    category.Url,
                                    true,
                                    store.Alias)
                                );
                            }
                        }
                    }

                    return list.Distinct();

                } catch(Exception ex)
                {
                    _logger.Error<CatalogUrlProvider>(ex, "EkomUrlProvider-GetOtherUrls Failed.");
                }

                return null;
            });
        }
    }
}
