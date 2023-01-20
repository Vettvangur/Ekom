using Ekom.API;
using Ekom.Interfaces;
using Ekom.Manager.Models;
using Ekom.Models;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Services;
using Umbraco.Web.Composing;
using Umbraco.Web.WebApi;

namespace Ekom.Controllers
{
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
    /// <summary>
    /// Private api, used by Ekom Manager
    /// </summary>
    [Umbraco.Web.Mvc.PluginController("Ekom")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2007:Consider calling ConfigureAwait on the awaited task",
        Justification = "Async controller actions don't need ConfigureAwait")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "Async controller action")]
    public class ManagerApiController : UmbracoAuthorizedApiController
    {
        readonly IManagerRepository _managerRepository;
        readonly IContentService _contentService;
        /// <summary>
        /// ctor
        /// </summary>
        public ManagerApiController(IManagerRepository managerRepository, IContentService contentService)
        {
            _contentService = contentService;
            _managerRepository = managerRepository;
        }

        public string GetNodeName(string nodeId)
        {
            IPublishedContent node = null;
            var helper = Current.UmbracoHelper;

            if (Udi.TryParse(nodeId, out Udi _udi))
            {
                node = helper.Content(_udi);
            }

            if (int.TryParse(nodeId, out int _int))
            {
                node = helper.Content(_int);
            }

            if (Guid.TryParse(nodeId, out Guid _guid))
            {
                node = helper.Content(_guid);
            }

            return node != null ? node.Name : "";
        }

        public async Task<IOrderInfo> GetOrder([FromUri] Guid uniqueId)
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
        public async Task<IEnumerable<OrderActivityLog>> GetActivityLog([FromUri] Guid orderId)
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
        public async Task<IEnumerable<OrderActivityLog>> GetLatestActivityLogs()
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
        public async Task<IEnumerable<OrderActivityLog>> GetLatestActivityLogsByUser([FromUri] string userName)
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
        public async Task<OrderListData> GetOrders()
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
        public async Task<OrderListData> GetAllOrders([FromUri] DateTime start, [FromUri] DateTime end)
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
        public async Task<OrderListData> SearchOrders([FromUri] DateTime start, [FromUri] DateTime end, [FromUri] string query = "", [FromUri] string store = "", [FromUri] string orderStatus = "", [FromUri] string payment = "", [FromUri] string shipping = "", [FromUri] string discount = "")
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
        public async Task<OrderListData> GetOrdersByStatus([FromUri] DateTime start, [FromUri] DateTime end, [FromUri] OrderStatus orderStatus)
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

        public async Task<PaymentData> GetPaymentData([FromUri] string orderId)
        {
            return await _managerRepository.GetPaymentData(orderId);
        }

        [HttpPost]
        public async Task<bool> UpdateStatus([FromUri]Guid orderId, [FromUri]int orderStatus, [FromUri] bool notification)
        {
            try
            {
                var status = (OrderStatus)orderStatus;

                await _managerRepository.UpdateStatusAsync(orderId, status, new ChangeOrderSettings
                {
                    FireEvents = notification,
                });

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
