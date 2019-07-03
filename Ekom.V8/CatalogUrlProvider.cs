using Ekom.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace EkomV8
{
    class CatalogUrlProvider : IUrlProvider
    {
        public string GetUrl(UmbracoContext umbracoContext, int id, Uri current, UrlProviderMode mode)
        {
            return UmbracoContext.Current.Application.ApplicationCache.RequestCache.GetCacheItem(
                "EkomUrlProvider-GetUrl-" + id,
                () =>
                {
                    try
                    {
                        var content = umbracoContext.ContentCache.GetById(id);

                        if (content == null ||
                            (content.DocumentTypeAlias != "ekmProduct" && content.DocumentTypeAlias != "ekmCategory")) return null;

                        var stores = API.Store.Instance.GetAllStores();

                        if (!stores.Any()) return null;

                        if (content.DocumentTypeAlias == "ekmProduct")
                        {
                            var product = API.Catalog.Instance.GetProduct(stores.First().Alias, id);

                            if (product != null)
                            {
                                return product.Url;
                            }

                        }
                        else
                        {
                            var category = API.Catalog.Instance.GetCategory(stores.First().Alias, id);

                            if (category != null)
                            {
                                return category.Url;
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("EkomUrlProvider Get Url Failed", ex);
                    }

                    return null;

                }) as string;


        }

        public IEnumerable<string> GetOtherUrls(UmbracoContext umbracoContext, int id, Uri current)
        {
            return UmbracoContext.Current.Application.ApplicationCache.RequestCache.GetCacheItem(
            "EkomUrlProvider-GetOtherUrls-" + id,
            () =>
            {
                try
                {
                    var content = umbracoContext.ContentCache.GetById(id);

                    if (content == null ||
                        (content.DocumentTypeAlias != "ekmProduct" && content.DocumentTypeAlias != "ekmCategory"))
                        return Enumerable.Empty<string>();

                    var list = new List<string>();

                    var stores = API.Store.Instance.GetAllStores().ToList();

                    if (stores.Count() <= 1) return list;

                    foreach (var store in stores.Skip(1))
                    {

                        if (content.DocumentTypeAlias == "ekmProduct")
                        {
                            var product = API.Catalog.Instance.GetProduct(store.Alias, id);

                            if (product != null)
                            {
                                list.Add(product.Url);
                            }

                        }
                        else
                        {
                            var category = API.Catalog.Instance.GetCategory(store.Alias, id);

                            if (category != null)
                            {
                                list.Add(category.Url);
                            }

                        }

                    }

                    return list.Distinct();

                } catch(Exception ex)
                {
                    Log.Error("EkomUrlProvider-GetOtherUrls Failed.", ex);
                }

                return null;

            }) as IEnumerable<string>;
        }
    }
}
