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
        /// <summary>
        /// Maps virtual URLs to IPublishedContent items
        /// Performs various request related processing
        /// F.x. determining the Store/Currency first from Cookie, then domain and then default
        /// </summary>
        public bool TryFindContent(PublishedContentRequest contentRequest)
        {
            var storeInfo = HttpContext.Current == null ? // Unnecessary?
                                                   null : 
                                                   HttpContext.Current.Request.Cookies["StoreInfo"];

            var path = contentRequest.Uri.GetAbsolutePathDecoded()
                                         .ToLower()
                                         .AddTrailing();


            #region Store

            Store store = null;
            string storeAlias = storeInfo != null ? storeInfo.Values["StoreAlias"] : null;

            // Attempt to retrieve Store from cookie data
            if (!string.IsNullOrEmpty(storeAlias))
            {
                store = StoreService.GetStoreByAlias(storeAlias);
            }

            // Get by Domain
            if (store == null && contentRequest.UmbracoDomain != null)
            {
                LogHelper.Info(GetType(), "CatalogContentFinder... TryFindContent... Path: " + 
                                            path + " Domain: " + 
                                            contentRequest.UmbracoDomain.DomainName);

                store = StoreService.GetStoreByDomain(contentRequest.UmbracoDomain.DomainName);
            }

            // Grab default store / First store from cache if no umbracoDomain present
            if (store == null)
            {
                store = StoreService.GetStoreByDomain();
            }

            LogHelper.Info(GetType(), "CatalogContentFinder... Store: " + store.Alias);

            #endregion


            #region Product and/or Category

            // Requesting Product?
            var product = ProductCache.Cache[store.Alias]
                                      .FirstOrDefault(x => x.Value.Urls.Contains(path))
                                      .Value;

            int contentId = 0;
            Category category;

            if (product != null)
            {
                contentId = product.Id;

                var urlArray         = path.Split('/');
                var categoryUrlArray = urlArray.Take(urlArray.Count() - 2);
                var categoryUrl      = string.Join("/", categoryUrlArray).AddTrailing();

                category = CategoryCache.Cache[store.Alias]
                                        .FirstOrDefault(x => x.Value.Urls.Contains(categoryUrl))
                                        .Value;
            }
            else // Request Category?
            {
                category = CategoryCache.Cache[store.Alias]
                                        .FirstOrDefault(x => x.Value.Urls.Contains(path))
                                        .Value;

                if (category != null)
                {
                    contentId = category.Id;
                }
                // else Requesting Neither
            }
            #endregion


            #region Currency 

            // Unfinished
            object Currency = storeInfo != null ? /* CurrencyHelper.Get(*/storeInfo.Values["Currency"] : null;

            #endregion


            var uwbsRequest = new ContentRequest
            {
                Store = store,
                Currency = Currency,
                DomainPrefix = path,
                Product = product,
                Category = category
            };

            var appCache = contentRequest.RoutingContext.UmbracoContext.Application.ApplicationCache;

            appCache.RequestCache.GetCacheItem("uwbsRequest", () => uwbsRequest);


            // Request for Product or Category
            if (contentId != 0)
            {
                var contentCache = UmbracoContext.Current.ContentCache;

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
