﻿using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;
using System.Web.Http;
using Umbraco.Web.WebApi;

namespace Ekom.Controllers
{
    /// <summary>
    /// Private api, used by Ekom Manager
    [Umbraco.Web.Mvc.PluginController("Ekom")]
    public class ManagerApiController : UmbracoAuthorizedApiController
    {
        IManagerRepository _managerRepository;
        /// <summary>
        /// ctor
        /// </summary>
        public ManagerApiController()
        {
            _managerRepository = Ekom.Configuration.container.GetInstance<IManagerRepository>();
        }

        /// <summary>
        /// List of orders.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<OrderData> GetOrders()
        {
            return _managerRepository.GetOrders();
        }

        /// <summary>
        /// List of orders.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<OrderData> GetAllOrders([FromUri] DateTime start, [FromUri] DateTime end)
        {
            return _managerRepository.GetAllOrders(start, end);
        }
        public IEnumerable<OrderData> GetOrdersByStatus([FromUri] DateTime start, [FromUri] DateTime end, [FromUri] OrderStatus orderStatus)
        {
            return _managerRepository.GetOrdersByStatus(start, end, orderStatus);
        }
    }
}
