using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Manager.Models;
using Ekom.Models.Data;
using log4net;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Web.Http;
using Umbraco.Web.WebApi;

namespace Ekom.Controllers
{
    /// <summary>
    /// Private api, used by Ekom Manager
    [Umbraco.Web.Mvc.PluginController("Ekom")]
    public class ManagerApiController : UmbracoAuthorizedApiController
    {
        readonly IManagerRepository _managerRepository;
        /// <summary>
        /// ctor
        /// </summary>
        public ManagerApiController()
        {
            _managerRepository = Ekom.Configuration.container.GetInstance<IManagerRepository>();
        }

        public string Get()
        {
            return "sup";
        }
        public IOrderInfo GetOrder([FromUri] Guid uniqueId)
        {
            return _managerRepository.GetOrder(uniqueId);

        }
        public IOrderInfo GetOrderInfo(Guid uniqueId)
        {
            return API.Order.Instance.GetOrder(uniqueId);
        }
        //public IEnumerable<OrderActivityLog> GetActivityLog([FromUri] Guid orderId)
        //{
        //    return _managerRepository.GetOrderActivityLog(orderId);
        //}
        //[HttpGet]
        //public IEnumerable<OrderActivityLog> GetLatestActivityLogs()
        //{
        //    return _managerRepository.GetLatestActivityLogs();
        //}
        //[HttpGet]
        //public IEnumerable<OrderActivityLog> GetLatestActivityLogsByUser([FromUri] Guid userId)
        //{
        //    return _managerRepository.GetLatestActivityLogsByUser(userId);
        //}
        /// <summary>
        /// List of orders.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public OrderListData GetOrders()
        {
            return _managerRepository.GetOrders();
        }

        /// <summary>
        /// List of orders.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public OrderListData GetAllOrders([FromUri] DateTime start, [FromUri] DateTime end)
        {
            return _managerRepository.GetAllOrders(start, end);
        }
        [HttpGet]
        public OrderListData SearchOrders([FromUri] DateTime start, [FromUri] DateTime end, [FromUri] string query = "", [FromUri] string store = "", [FromUri] string orderStatus = "", [FromUri] string payment = "", [FromUri] string shipping = "", [FromUri] string discount = "")
        {
            return _managerRepository.SearchOrders(start, end, query, store, orderStatus, payment, shipping, discount);
        }
        public OrderListData GetOrdersByStatus([FromUri] DateTime start, [FromUri] DateTime end, [FromUri] OrderStatus orderStatus)
        {
            return _managerRepository.GetOrdersByStatus(start, end, orderStatus);
        }

        [HttpPost]
        public bool UpdateStatus([FromUri]Guid orderId, [FromUri]int orderStatus)
        {
            var status = (OrderStatus)orderStatus;

            _managerRepository.UpdateStatus(orderId, status);

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

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
