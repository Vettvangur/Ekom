using Ekom.Utilities;
using Ekom.Interfaces;
using Ekom.Manager.Models;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Http;
using Umbraco.Core;
using Umbraco.Core.Composing;
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
            return await _managerRepository.GetOrderAsync(uniqueId);
        }
        public IOrderInfo GetOrderInfo(Guid uniqueId)
        {
            return API.Order.Instance.GetOrder(uniqueId);
        }
        [HttpGet]
        public async Task<IEnumerable<OrderActivityLog>> GetActivityLogAsync([FromUri] Guid orderId)
        {
            return await _managerRepository.GetLogsAsync(orderId);
        }
        [HttpGet]
        public async Task<IEnumerable<OrderActivityLog>> GetLatestActivityLogsAsync()
        {
            return await _managerRepository.GetLatestActivityLogsAsync();
        }
        [HttpGet]
        public async Task<IEnumerable<OrderActivityLog>> GetLatestActivityLogsByUserAsync([FromUri] string userName)
        {
            return await _managerRepository.GetLatestActivityLogsByUserAsync(userName);
        }
        /// <summary>
        /// List of orders.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<OrderListData> GetOrdersAsync()
        {
            return await _managerRepository.GetOrdersAsync();
        }

        /// <summary>
        /// List of orders.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<OrderListData> GetAllOrdersAsync([FromUri] DateTime start, [FromUri] DateTime end)
        {
            return await _managerRepository.GetAllOrdersAsync(start, end);
        }
        [HttpGet]
        public async Task<OrderListData> SearchOrdersAsync([FromUri] DateTime start, [FromUri] DateTime end, [FromUri] string query = "", [FromUri] string store = "", [FromUri] string orderStatus = "", [FromUri] string payment = "", [FromUri] string shipping = "", [FromUri] string discount = "")
        {
            return await _managerRepository.SearchOrdersAsync(start, end, query, store, orderStatus, payment, shipping, discount);
        }
        public async Task<OrderListData> GetOrdersByStatusAsync([FromUri] DateTime start, [FromUri] DateTime end, [FromUri] OrderStatus orderStatus)
        {
            return await _managerRepository.GetOrdersByStatusAsync(start, end, orderStatus);
        }

        [HttpPost]
        public async Task<bool> UpdateStatusAsync([FromUri]Guid orderId, [FromUri]int orderStatus)
        {
            var status = (OrderStatus)orderStatus;

            await _managerRepository.UpdateStatusAsync(orderId, status);

            return true;
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
