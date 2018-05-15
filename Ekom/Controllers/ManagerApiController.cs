﻿using Ekom.Domain.Repositories;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;
using Umbraco.Web.WebApi;

namespace Ekom.Controllers
{
    /// <summary>
    /// Private api, used by Ekom Manager
    [Umbraco.Web.Mvc.PluginController("Ekom")]
    public class ManagerApiController : UmbracoAuthorizedApiController
    {
        IManagerRepository _managerRepository;
        IOrderRepository _orderRepository;
        /// <summary>
        /// ctor
        /// </summary>
        public ManagerApiController()
        {
            _managerRepository = Ekom.Configuration.container.GetInstance<IManagerRepository>();
            _orderRepository = Ekom.Configuration.container.GetInstance<IOrderRepository>();
        }

        /// <summary>
        /// List of orders.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<OrderData> GetOrders()
        {
            return _orderRepository.GetOrdersByStatus(Helpers.OrderStatus.Incomplete);
        }
    }
}
