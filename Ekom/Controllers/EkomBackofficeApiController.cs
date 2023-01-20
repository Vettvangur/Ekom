#if NETFRAMEWORK
using System.Web.Http;
#else
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
#endif
using Ekom.Exceptions;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Ekom.Services;
using Ekom.Authorization;

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
#if NETFRAMEWORK

    public class EkomBackofficeApiController : ApiController
    {
        /// <summary>
        /// 
        /// </summary>
        public EkomBackofficeApiController(Configuration config, IUmbracoService umbracoService, IMetafieldService metafieldService)
        {
        }
#else
    [Route("ekom/backoffice")]
    public class EkomBackofficeApiController : ControllerBase
    {
        /// <summary>
        /// 
        /// </summary>
        public EkomBackofficeApiController(Configuration config, IUmbracoService umbracoService, IMetafieldService metafieldService)
        {
            _config = config;
            _umbracoService = umbracoService;
            _metafieldService = metafieldService;
        }
#endif

        readonly Configuration _config;
        readonly IUmbracoService _umbracoService;
        readonly IMetafieldService _metafieldService;

        [HttpGet]
        [Route("GetNonEkomDataTypes")]
        [UmbracoUserAuthorize]
        public IEnumerable<object> GetNonEkomDataTypes()
            => _umbracoService.GetNonEkomDataTypes();

        [HttpGet]
        [Route("DataType/{id:guid}")]
        [UmbracoUserAuthorize]
        public object GetDataTypeById(Guid id)
            => _umbracoService.GetDataTypeById(id);


        [HttpGet]
        [Route("DataType/{contentTypeAlias}/propertyAlias/{propertyAlias}")]
        [UmbracoUserAuthorize]
        public object GetDataTypeByAlias(
            string contentTypeAlias,
            string propertyAlias)
            => _umbracoService.GetDataTypeByAlias(contentTypeAlias, propertyAlias);

        [HttpGet]
        [Route("Metafields")]
        [UmbracoUserAuthorize]
        public object GetMetafields()
    => _metafieldService.GetMetafields();

        [HttpGet]
        [Route("Languages")]
        [UmbracoUserAuthorize]
        public IEnumerable<object> GetLanguages()
            => _umbracoService.GetLanguages();

        [HttpGet]
        [Route("Stores")]
        [UmbracoUserAuthorize]
        public IEnumerable<object> GetStores()
        {
            var stores = API.Store.Instance.GetAllStores();

            return stores;
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
            foreach (var cacheEntry in _config.CacheList.Value)
            {
                cacheEntry.FillCache();
            }

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
        public async Task InsertCoupon(string couponCode, int numberAvailable, Guid discountId)
        {
            try
            {
                await API.Order.Instance.InsertCouponCodeAsync(couponCode, numberAvailable, discountId);

                throw new HttpResponseException(HttpStatusCode.OK);
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                throw ExceptionHandler.Handle<HttpResponseException>(ex);
            }
        }

        /// <summary>
        /// Remove Coupon
        /// </summary>
        [HttpDelete]
        [Route("coupon/{couponCode}/discountId/{id:Guid}")]
        [UmbracoUserAuthorize]
        public async Task RemoveCoupon(string couponCode, Guid discountId)
        {
            try
            {
                await API.Order.Instance.RemoveCouponCodeAsync(couponCode, discountId);

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
        public async Task<object> GetCouponsForDiscount(Guid discountId)
        {
            try
            {
                var items = await API.Order.Instance.GetCouponsForDiscountAsync(discountId);

                return items;
            }
            catch (Exception ex)
            {
                throw ExceptionHandler.Handle<HttpResponseException>(ex);
            }
        }
    }
}
