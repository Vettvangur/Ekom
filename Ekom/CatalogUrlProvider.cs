using Ekom.Services;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Ekom
{
    class CatalogUrlProvider : IUrlProvider
    {
        public string GetUrl(UmbracoContext umbracoContext, int id, Uri current, UrlProviderMode mode)
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
                        
            } else
            {
                var category = API.Catalog.Instance.GetCategory(stores.First().Alias, id);

                if (category != null)
                {
                    return category.Url;
                }
                        
            }

            return null;
        }

        public IEnumerable<string> GetOtherUrls(UmbracoContext umbracoContext, int id, Uri current)
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

        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
