using log4net;
using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Umbraco.Web.Mvc;
using uWebshop.App_Start;
using uWebshop.Helpers;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshopSite.Extensions.Controllers
{
    /// <summary>
    /// Handles order/cart creation, updates and removals
    /// </summary>
    [PluginController("uWebshop")]
    public class OrderController : SurfaceController
    {
        /// <summary>
        /// Add product to order
        /// </summary>
        /// <param name="productId">Guid Key of product</param>
        /// <param name="storeAlias"></param>
        /// <param name="quantity"></param>
        /// <param name="variantId"></param>
        /// <param name="action">Add/Update</param>
        /// <returns></returns>
        public JsonResult AddToOrder(OrderRequest request)
        {
            try
            {
                var os = uWebshop.API.Order.Current;

                var variantIds = new List<Guid>();

                if (request.variantId != null && request.variantId != Guid.Empty)
                {
                    variantIds.Add(request.variantId.Value);
                }

                var orderInfo = os.AddOrderLine(request.productId, variantIds, request.quantity, request.storeAlias, request.action);

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
                var os = uWebshop.API.Order.Current;

                var orderInfo = os.AddOrderLine(lineId, null, quantity, storeAlias, null);

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
                var os = uWebshop.API.Order.Current;

                var orderInfo = os.RemoveOrderLine(lineId, storeAlias);

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
