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
using log4net;
using System.Reflection;
using System.Configuration;

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
            try
            {
                var umbracoContext = contentRequest.RoutingContext.UmbracoContext;
                var httpContext = umbracoContext.HttpContext;
                var umbracoHelper = new UmbracoHelper(umbracoContext);

                // Allows for configuration of content nodes to use for matching all requests
                // Use case: uWebshop populated by adapter, used as in memory cache with no backing umbraco nodes
                var virtualContent = ConfigurationManager.AppSettings["virtualContent"];

                var path = contentRequest.Uri
                                         .GetAbsolutePathDecoded()
                                         .ToLower()
                                         .AddTrailing();

                Store store = StoreService.GetStore(contentRequest.UmbracoDomain, httpContext);


                #region Product and/or Category

                // Requesting Product?
                var product = ProductCache.Cache[store.Alias]
                                      .FirstOrDefault(x => x.Value.Urls != null &&
                                                           x.Value.Urls.Contains(path))
                                      .Value;

                int contentId = 0;
                Category category;

                if (product != null)
                {
                    if (virtualContent.InvariantEquals("true"))
                    {
                        contentId = int.Parse(umbracoHelper.GetDictionaryValue("virtualProductNode"));
                    }
                    else
                    {
                        contentId = product.Id;
                    }

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
                                            .FirstOrDefault(x => x.Value.Urls != null &&
                                                                 x.Value.Urls.Contains(path))
                                            .Value;

                    if (category != null)
                    {
                        if (virtualContent.InvariantEquals("true"))
                        {
                            contentId = int.Parse(umbracoHelper.GetDictionaryValue("virtualCategoryNode"));
                        }
                        else
                        {
                            contentId = category.Id;
                        }
                    }
                    // else Requesting Neither
                }
                #endregion


                #region Currency 

                // Unfinished - move to currency service

                HttpCookie storeInfo = httpContext.Request.Cookies["StoreInfo"];

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

                var appCache = umbracoContext.Application.ApplicationCache;

                appCache.RequestCache.GetCacheItem("uwbsRequest", () => uwbsRequest);


                // Unfinished
                //var order = (BasketService) httpContext.Session["uwbsBasket"];

                //HttpCookie OrderInfo = httpContext.Request.Cookies["OrderInfo"];

                //new Order(OrderInfo);

                //appCache.RequestCache.GetCacheItem("Order", () => order);


                // Request for Product or Category
                if (contentId != 0)
                {
                    var contentCache = umbracoContext.ContentCache;
                    
                    var content = contentCache.GetById(contentId);

                    if (content != null)
                    {
                        contentRequest.PublishedContent = content;

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error trying to find matching content for request", ex);
            }

            return false;
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
