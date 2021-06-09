using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;

namespace Ekom.Controllers
{
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
    /// <summary>
    /// Public api, used by property editors
    /// </summary>
    [PluginController("Ekom")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2007:Consider calling ConfigureAwait on the awaited task",
        Justification = "Async controller actions don't need ConfigureAwait")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "Async controller action")]
    public class ApiController : UmbracoAuthorizedApiController
    {
        readonly ICountriesRepository _countriesRepo;
        readonly Configuration _config;
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="countriesRepo"></param>
        public ApiController(Configuration config, ICountriesRepository countriesRepo)
        {
            _config = config;
            _countriesRepo = countriesRepo;
        }

        public List<Country> GetCountries()
        {
            return _countriesRepo.GetAllCountries();
        }

        /// <summary>
        /// List of all stores
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IStore> GetAllStores()
        {
            return API.Store.Instance.GetAllStores();
        }

        /// <summary>
        /// Repopulates all Ekom cache
        /// </summary>
        /// <returns></returns>
        public object PopulateCache()
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
        public object GetConfig()
        {
            return Ekom.Configuration.Current;
        }


        /// <summary>
        /// Get Stock By Store
        /// </summary>
        /// <returns></returns>
        public int GetStockByStore(Guid id, string storeAlias)
        {
            return API.Stock.Instance.GetStock(id, storeAlias);
        }

        /// <summary>
        /// Get Stock 
        /// </summary>
        /// <returns></returns>
        public int GetStock(Guid id)
        {
            return API.Stock.Instance.GetStock(id);
        }

        /// <summary>
        /// Increment stock count of item. 
        /// If PerStoreStock is configured, gets store from cache and updates relevant item.
        /// If no stock entry exists, creates a new one, then attempts to update.
        /// </summary>
        [HttpPost]
        public async Task<HttpResponseMessage> IncrementStock(Guid id, int stock)
        {
            try
            {
                await API.Stock.Instance.IncrementStockAsync(id, stock);

                // ToDo: Log Umbraco user performing the action
                //Logger.Info<ApiController>("")

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return ExceptionHandler.Handle<HttpResponseMessage>(ex);
            }
        }

        /// <summary>
        /// Increment stock count of store item. 
        /// If no stock entry exists, creates a new one, then attempts to update.
        /// </summary>
        [HttpPost]
        public async Task<HttpResponseMessage> IncrementStock(Guid id, string storeAlias, int stock)
        {
            try
            {
                await API.Stock.Instance.IncrementStockAsync(id, storeAlias, stock);

                // ToDo: Log Umbraco user performing the action

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return ExceptionHandler.Handle<HttpResponseMessage>(ex);
            }
        }

        /// <summary>
        /// Sets stock count of item. 
        /// If PerStoreStock is configured, gets store from cache and updates relevant item.
        /// If no stock entry exists, creates a new one, then attempts to update.
        /// </summary>
        [HttpPost]
        public async Task<HttpResponseMessage> SetStock(Guid id, int stock)
        {
            try
            {
                await API.Stock.Instance.SetStockAsync(id, stock);

                // ToDo: Log Umbraco user performing the action

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return ExceptionHandler.Handle<HttpResponseMessage>(ex);
            }
        }

        /// <summary>
        /// Sets stock count of store item. 
        /// If no stock entry exists, creates a new one, then attempts to update.
        /// </summary>
        [HttpPost]
        public async Task<HttpResponseMessage> SetStock(Guid id, string storeAlias, int stock)
        {
            try
            {
                await API.Stock.Instance.SetStockAsync(id, storeAlias, stock);

                // ToDo: Log Umbraco user performing the action

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return ExceptionHandler.Handle<HttpResponseMessage>(ex);
            }
        }

        /// <summary>
        /// Insert Coupon
        /// </summary>
        [HttpPost]
        public async Task<HttpResponseMessage> InsertCoupon(string couponCode, int numberAvailable, Guid discountId)
        {
            try
            {
                await API.Order.Instance.InsertCouponCodeAsync(couponCode, numberAvailable, discountId);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return ExceptionHandler.Handle<HttpResponseMessage>(ex);
            }
        }

        /// <summary>
        /// Remove Coupon
        /// </summary>
        [HttpPost]
        public async Task<HttpResponseMessage> RemoveCoupon(string couponCode, Guid discountId)
        {
            try
            {
                await API.Order.Instance.RemoveCouponCodeAsync(couponCode, discountId);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return ExceptionHandler.Handle<HttpResponseMessage>(ex);
            }
        }

        /// <summary>
        /// Get Coupons for Discount
        /// </summary>
        [HttpPost]
        public async Task<HttpResponseMessage> GetCouponsForDiscount(Guid discountId)
        {
            try
            {
                var items = await API.Order.Instance.GetCouponsForDiscountAsync(discountId);

                return Request.CreateResponse(HttpStatusCode.OK, items);
            }
            catch (Exception ex)
            {
                return ExceptionHandler.Handle<HttpResponseMessage>(ex);
            }
        }
    }
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
}
