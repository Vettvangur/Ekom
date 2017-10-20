using Ekom.Models;
using Ekom.Services;
using log4net;
using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Umbraco.Web.Mvc;

namespace Ekom.Controllers
{
    /// <summary>
    /// Handles order/cart creation, updates and removals
    /// </summary>
    [PluginController("Ekom")]
    public class OrderController : SurfaceController
    {
        ILog _log;
        OrderService _orderSvc;
        /// <summary>
        /// ctor
        /// We can't hook into the MVC DI resolver since that would override consumer resolvers.
        /// </summary>
        public OrderController()
        {
            _orderSvc = Configuration.container.GetInstance<OrderService>();

            var logFac = Configuration.container.GetInstance<ILogFactory>();
            _log = logFac.GetLogger(typeof(OrderController));
        }

        /// <summary>
        /// Add product to order
        /// </summary>
        /// <param name="request">Guid Key of product</param>
        /// <returns></returns>
        public JsonResult AddToOrder(OrderRequest request)
        {
            try
            {
                var variantIds = new List<Guid>();

                if (request.variantId != null && request.variantId != Guid.Empty)
                {
                    variantIds.Add(request.variantId.Value);
                }

                var orderInfo = _orderSvc.AddOrderLine(request.productId, variantIds, request.quantity, request.storeAlias, request.action);

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });
            }
            catch (Exception ex)
            {

                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update order, change quantity of line in cart/order
        /// </summary>
        /// <param name="lineId">Guid Key of line to update</param>
        /// <param name="storeAlias"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        public JsonResult UpdateOrder(Guid lineId, string storeAlias, int quantity)
        {
            try
            {
                var orderInfo = _orderSvc.AddOrderLine(lineId, null, quantity, storeAlias, null);

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Remove product from order/cart
        /// </summary>
        /// <param name="lineId">Guid Key of product/line</param>
        /// <param name="storeAlias"></param>
        /// <returns></returns>
        public JsonResult RemoveOrderLine(Guid lineId, string storeAlias)
        {
            try
            {
                var orderInfo = _orderSvc.RemoveOrderLine(lineId, storeAlias);

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });
            }
            catch (Exception ex)
            {

                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }
    }
}
