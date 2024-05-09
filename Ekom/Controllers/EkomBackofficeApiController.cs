using Ekom.ActionFilters;
using Ekom.Authorization;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Net;

namespace Ekom.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2007:Consider calling ConfigureAwait on the awaited task",
        Justification = "Async controller actions don't need ConfigureAwait")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "Async controller action")]

    [Route("ekom/backoffice")]
    [CamelCaseJson]
    public class EkomBackofficeApiController : ControllerBase
    {
        private readonly Configuration _config;
        private readonly IUmbracoService _umbracoService;
        private readonly IMetafieldService _metafieldService;
        private readonly INodeService _nodeService;
        private readonly IMemoryCache _memoryCache;

        public EkomBackofficeApiController(Configuration config, IUmbracoService umbracoService, IMetafieldService metafieldService, INodeService nodeService, IMemoryCache memoryCache)
        {
            _config = config;
            _umbracoService = umbracoService;
            _metafieldService = metafieldService;
            _nodeService = nodeService;
            _memoryCache = memoryCache;
        }


        [HttpGet]
        [Route("GetNonEkomDataTypes")]
        [UmbracoUserAuthorize]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public IEnumerable<object> GetNonEkomDataTypes()
            => _umbracoService.GetNonEkomDataTypes();

        [HttpGet]
        [Route("DataType/{id:guid}")]
        [UmbracoUserAuthorize]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public object GetDataTypeById(Guid id)
            => _umbracoService.GetDataTypeById(id);


        [HttpGet]
        [Route("DataType/{contentTypeAlias}/propertyAlias/{propertyAlias}")]
        [UmbracoUserAuthorize]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public object GetDataTypeByAlias(
            string contentTypeAlias,
            string propertyAlias)
            => _umbracoService.GetDataTypeByAlias(contentTypeAlias, propertyAlias);

        [HttpGet]
        [Route("Metafields")]
        [UmbracoUserAuthorize]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public IEnumerable<Metafield> GetMetafields()
            => _metafieldService.GetMetafields();

        [HttpGet]
        [Route("Languages")]
        [UmbracoUserAuthorize]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public IEnumerable<object> GetLanguages()
            => _umbracoService.GetLanguages();

        [HttpGet]
        [Route("Stores")]
        [UmbracoUserAuthorize]
        public async Task<IEnumerable<IStore>> GetAllStores()
        {
            return await _memoryCache.GetOrCreateAsync("AllStores", async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
                return API.Store.Instance.GetAllStores();
            });
        }


        [HttpGet]
        [Route("Stores/{id}")]
        [UmbracoUserAuthorize]
        public async Task<IEnumerable<IStore>> GetStores([FromRoute] int id)
        {
            return await _memoryCache.GetOrCreateAsync($"Stores_{id}", async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);

                var stores = API.Store.Instance.GetAllStores();
                var node = _nodeService.NodeById(id);

                if (node != null)
                {
                    stores = stores.Where(store => !node.IsItemDisabled(store));
                }

                return stores;
            });
        }

        /// <summary>
        /// Repopulates all Ekom cache
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("Cache")]
        [UmbracoUserAuthorize]
        public bool PopulateCache()
        {
            API.Store.Instance.RefreshCache();

            return true;
        }

        /// <summary>
        /// Get Config
        /// </summary>
        [HttpGet]
        [Route("Config")]
        [UmbracoUserAuthorize]
        public Configuration GetConfig()
        {
            return _config;
        }

        /// <summary>
        /// Get Stock By Store
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("Stock/{id:Guid}/StoreAlias/{storeAlias}")]
        [UmbracoUserAuthorize]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public int GetStockByStore(Guid id, string storeAlias)
        {
            return API.Stock.Instance.GetStock(id, storeAlias);
        }

        /// <summary>
        /// Get Stock 
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("Stock/{id:Guid}")]
        [UmbracoUserAuthorize]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public int GetStock(Guid id)
        {
            return API.Stock.Instance.GetStock(id);
        }

        /// <summary>
        /// Increment stock count of item. 
        /// If PerStoreStock is configured, gets store from cache and updates relevant item.
        /// If no stock entry exists, creates a new one, then attempts to update.
        /// </summary>
        [HttpPatch]
        [Route("stock/{id:Guid}/value/{stock}")]
        [UmbracoUserAuthorize]
        public async Task<HttpResponseException> IncrementStock(Guid id, int stock)
        {
            try
            {
                await API.Stock.Instance.IncrementStockAsync(id, stock);

                // ToDo: Log Umbraco user performing the action
                //Logger.Info<ApiController>("")

                throw new HttpResponseException(HttpStatusCode.OK);
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                throw ExceptionHandler.Handle<HttpResponseException>(ex);
            }
        }

        /// <summary>
        /// Increment stock count of store item. 
        /// If no stock entry exists, creates a new one, then attempts to update.
        /// </summary>
        [HttpPatch]
        [Route("stock/{id:Guid}/StoreAlias/{storeAlias}/value/{stock}")]
        [UmbracoUserAuthorize]
        public async Task IncrementStock(Guid id, string storeAlias, int stock)
        {
            try
            {
                await API.Stock.Instance.IncrementStockAsync(id, storeAlias, stock);

                // ToDo: Log Umbraco user performing the action

                throw new HttpResponseException(HttpStatusCode.OK);
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                throw ExceptionHandler.Handle<HttpResponseException>(ex);
            }
        }

        /// <summary>
        /// Sets stock count of item. 
        /// If PerStoreStock is configured, gets store from cache and updates relevant item.
        /// If no stock entry exists, creates a new one, then attempts to update.
        /// </summary>
        [HttpPut]
        [Route("stock/{id:Guid}/value/{stock}")]
        [UmbracoUserAuthorize]
        public async Task SetStock(Guid id, int stock)
        {
            try
            {
                await API.Stock.Instance.SetStockAsync(id, stock);

                // ToDo: Log Umbraco user performing the action

                throw new HttpResponseException(HttpStatusCode.OK);
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                throw ExceptionHandler.Handle<HttpResponseException>(ex);
            }
        }

        /// <summary>
        /// Sets stock count of store item. 
        /// If no stock entry exists, creates a new one, then attempts to update.
        /// </summary>
        [HttpPut]
        [Route("stock/{id:Guid}/StoreAlias/{storeAlias}/value/{stock}")]
        [UmbracoUserAuthorize]
        public async Task SetStock(Guid id, string storeAlias, int stock)
        {
            try
            {
                await API.Stock.Instance.SetStockAsync(id, storeAlias, stock);

                // ToDo: Log Umbraco user performing the action

                throw new HttpResponseException(HttpStatusCode.OK);
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                throw ExceptionHandler.Handle<HttpResponseException>(ex);
            }
        }

        /// <summary>
        /// Insert Coupon
        /// </summary>
        [HttpPost]
        [Route("coupon/{couponCode}/NumberAvailable/{numberAvailable}/discountId/{id:Guid}")]
        [UmbracoUserAuthorize]
        public async Task InsertCoupon(string couponCode, int numberAvailable, Guid id)
        {
            try
            {
                await API.Order.Instance.InsertCouponCodeAsync(couponCode, numberAvailable, id);

                throw new HttpResponseException(HttpStatusCode.OK);
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                if (ex.Message == "Duplicate coupon")
                {
                    throw new HttpResponseException(HttpStatusCode.Conflict);
                }

                throw ExceptionHandler.Handle<HttpResponseException>(ex);
            }
        }

        /// <summary>
        /// Remove Coupon
        /// </summary>
        [HttpDelete]
        [Route("coupon/{couponCode}/discountId/{id:Guid}")]
        [UmbracoUserAuthorize]
        public async Task RemoveCoupon(string couponCode, Guid id)
        {
            try
            {
                await API.Order.Instance.RemoveCouponCodeAsync(couponCode, id);

                throw new HttpResponseException(HttpStatusCode.OK);
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                throw ExceptionHandler.Handle<HttpResponseException>(ex);
            }
        }

        /// <summary>
        /// Get Coupons for Discount
        /// </summary>
        [HttpGet]
        [Route("coupon/discountId/{id:Guid}")]
        [UmbracoUserAuthorize]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<object> GetCouponsForDiscount(Guid id, string query = "", int page = 1, int pageSize = 20)
        {
            try
            {
                var items = await API.Order.Instance.GetCouponsForDiscountAsync(id, query, page, pageSize);

                return items;
            }
            catch (Exception ex)
            {
                throw ExceptionHandler.Handle<HttpResponseException>(ex);
            }
        }
    }
}
