using Ekom.Interfaces;
using Ekom.Manager.Models;
using Ekom.Models.Data;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using Umbraco.Web.WebApi;

namespace Ekom.Controllers
{
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
    /// <summary>
    /// Private api, used by Ekom Manager
    [Umbraco.Web.Mvc.PluginController("Ekom")]
    public class ManagerApiController : UmbracoAuthorizedApiController
    {
        readonly IManagerRepository _managerRepository;
        /// <summary>
        /// ctor
        /// </summary>
        public ManagerApiController(IManagerRepository managerRepository)
        {
            _managerRepository = managerRepository;
        }

        public async Task<IOrderInfo> GetOrderAsync([FromUri] Guid uniqueId)
        {
            try
            {
                return await _managerRepository.GetOrderAsync(uniqueId);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }
        public IOrderInfo GetOrderInfo(Guid uniqueId)
        {
            try
            {
                return API.Order.Instance.GetOrder(uniqueId);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }
        [HttpGet]
        public async Task<IEnumerable<OrderActivityLog>> GetActivityLogAsync([FromUri] Guid orderId)
        {
            try
            {
                return await _managerRepository.GetLogsAsync(orderId);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }
        [HttpGet]
        public async Task<IEnumerable<OrderActivityLog>> GetLatestActivityLogsAsync()
        {
            try
            {
                return await _managerRepository.GetLatestActivityLogsAsync();
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }
        [HttpGet]
        public async Task<IEnumerable<OrderActivityLog>> GetLatestActivityLogsByUserAsync([FromUri] string userName)
        {
            try
            {
                return await _managerRepository.GetLatestActivityLogsByUserAsync(userName);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }
        /// <summary>
        /// List of orders.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<OrderListData> GetOrdersAsync()
        {
            try
            {
                return await _managerRepository.GetOrdersAsync();
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }

        /// <summary>
        /// List of orders.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<OrderListData> GetAllOrdersAsync([FromUri] DateTime start, [FromUri] DateTime end)
        {
            try
            {
                return await _managerRepository.GetAllOrdersAsync(start, end);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }
        [HttpGet]
        public async Task<OrderListData> SearchOrdersAsync([FromUri] DateTime start, [FromUri] DateTime end, [FromUri] string query = "", [FromUri] string store = "", [FromUri] string orderStatus = "", [FromUri] string payment = "", [FromUri] string shipping = "", [FromUri] string discount = "")
        {
            try
            {
                return await _managerRepository.SearchOrdersAsync(start, end, query, store, orderStatus, payment, shipping, discount);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }
        public async Task<OrderListData> GetOrdersByStatusAsync([FromUri] DateTime start, [FromUri] DateTime end, [FromUri] OrderStatus orderStatus)
        {
            try
            {
                return await _managerRepository.GetOrdersByStatusAsync(start, end, orderStatus);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }

        [HttpPost]
        public async Task<bool> UpdateStatusAsync([FromUri]Guid orderId, [FromUri]int orderStatus)
        {
            try
            {
                var status = (OrderStatus)orderStatus;

                await _managerRepository.UpdateStatusAsync(orderId, status);

                return true;
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }
        public IEnumerable<IStore> GetStores()
        {
            return _managerRepository.GetStores();
        }
        public object GetStoreList()
        {
            return _managerRepository.GetStoreList();
        }
        public IEnumerable<IShippingProvider> GetShippingProviders()
        {
            return _managerRepository.GetShippingProviders();
        }
        public IEnumerable<IPaymentProvider> GetPaymentProviders()
        {
            return _managerRepository.GetPaymentProviders();
        }
        public IEnumerable<IDiscount> GetDiscounts()
        {
            return _managerRepository.GetDiscounts();
        }

        public object GetStatusList()
        {
            return _managerRepository.GetStatusList();
        }
    }
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
}
