using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Web.Routing;
using Umbraco.Core.Logging;
using Umbraco.Web;
using uWebshop.Services;
using uWebshop.Cache;
using System.Web;
using uWebshop.Models;
using uWebshop.Utilities;

namespace uWebshop
{
    public class CatalogContentFinder : IContentFinder
    {
        public bool TryFindContent(PublishedContentRequest contentRequest)
        {

            var path = contentRequest.Uri.GetAbsolutePathDecoded().ToLower().AddTrailing();
            
            LogHelper.Info(GetType(), "CatalogContentFinder... TryFindContent... Path: " + path + " Domain: " + contentRequest.UmbracoDomain.DomainName );

            var store = StoreService.GetStoreByDomain(contentRequest.UmbracoDomain.DomainName);

            LogHelper.Info(GetType(), "CatalogContentFinder... Store: " + store.Alias);

            var contentCache = UmbracoContext.Current.ContentCache;

            var contentId = 0;

            Category category = null;

            var product = ProductCache._productCache.FirstOrDefault(x => x.Value.Store.Alias == store.Alias && x.Value.Urls.Contains(path)).Value;

            if (product != null)
            {
                contentId = product.Id;

                var urlArray = path.Split('/');
                var categoryUrlArray = urlArray.Take(urlArray.Count() - 2);
                var categoryUrl = string.Join("/", categoryUrlArray).AddTrailing();

                category = CategoryCache._categoryCache.FirstOrDefault(x => x.Value.Store.Alias == store.Alias && x.Value.Urls.Contains(categoryUrl)).Value;

            }

            if (product == null)
            {
                category = CategoryCache._categoryCache.FirstOrDefault(x => x.Value.Store.Alias == store.Alias && x.Value.Urls.Contains(path)).Value;

                if (category != null)
                {
                    contentId = category.Id;
                }

            }

            var request = new ContentRequest()
            {
                Store = store,
                DomainPrefix = path,
                Product = product,
                Category = category
            };

            HttpContext.Current.Cache["uwbsRequest"] = request;

            if (contentId != 0) {
                var content = contentCache.GetById(contentId);

                if (content != null)
                {
                    contentRequest.PublishedContent = content;

                    return true;

                }
            }

            return false;

        }
    }
}
