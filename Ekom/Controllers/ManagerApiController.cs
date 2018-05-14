using Ekom.Domain.Repositories;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;

namespace Ekom.Controllers
{
    /// <summary>
    /// Private api, used by Ekom Manager
    [PluginController("Ekom")]
    [UmbracoAuthorize]
    public class ManagerApiController : UmbracoApiController
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
    }
}
