using Ekom.API;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using Umbraco.Web.Mvc;

namespace Ekom.Controllers
{
    /// <summary>
    /// Handles order/cart creation, updates and removals
    /// </summary>
    [PluginController("Ekom")]
    public partial class OrderController : SurfaceController
    {
        ILog _log;
        /// <summary>
        /// ctor
        /// We can't hook into the MVC DI resolver since that would override consumer resolvers.
        /// </summary>
        public OrderController()
        {
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

                var orderInfo = Order.Instance.AddOrderLine(request.productId, variantIds, request.quantity, request.storeAlias, request.action);

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });
            }
            catch (Exception ex)
            {
                _log.Error("Failed to add to order!", ex);

                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Add product with multiple variants to order
        /// </summary>
        /// <param name="request">OrderMultipleRequest</param>
        /// <returns></returns>
        public JsonResult AddMultipleToOrder(OrderMultipleRequest request)
        {
            try
            {
                var os = Order.Instance;

                _log.Info("Add To Multiple Order: " + request.productId);

                if (request.variant.Any())
                {
                    IOrderInfo o = null;

                    var variantIds = new List<Guid>();

                    foreach (var variant in request.variant)
                    {
                        _log.Info("Add To Multiple Order: Variant: " + variant.Id + " - " + variant.Quantity);

                        if (Guid.TryParse(variant.Id.ToString(), out Guid variantId))
                        {
                            var items = new List<Guid>
                            {
                                variantId
                            };

                            o = os.AddOrderLine(request.productId, items, variant.Quantity, request.storeAlias, request.action);

                        }
                    }

                    return Json(new
                    {
                        success = true,
                        orderInfo = o
                    });

                }

                return Json(new
                {
                    success = false
                });

            }

            catch (Exception ex)
            {
                _log.Error("Add to multiple order Failed!", ex);

                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update Customer Information to Order
        /// </summary>
        /// <param name="form">FormData</param>
        /// <returns></returns>
        public JsonResult UpdateCustomerInformation(FormCollection form)
        {
            try
            {
                var orderInfo = Order.Instance.UpdateCustomerInformation(form.AllKeys.ToDictionary(k => k, v => form[v]));

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
        /// Update Shipping Information
        /// </summary>
        /// <returns></returns>
        public JsonResult UpdateShippingProvider(Guid ShippingProvider, string storeAlias)
        {
            try
            {
                var orderInfo = Order.Instance.UpdateShippingInformation(ShippingProvider, storeAlias);

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
        /// Update Payment Information
        /// </summary>
        /// <returns></returns>
        public JsonResult UpdatePaymentProvider(Guid PaymentProvider, string storeAlias)
        {
            try
            {
                var orderInfo = Order.Instance.UpdatePaymentInformation(PaymentProvider, storeAlias);

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
                var orderInfo = Order.Instance.AddOrderLine(lineId, null, quantity, storeAlias, null);

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
        public ActionResult RemoveOrderLine(Guid lineId, string storeAlias)
        {
            try
            {
                var orderInfo = Order.Instance.RemoveOrderLine(lineId, storeAlias);

                return Json(new { orderInfo, date = DateTime.Now });
            }
            catch (Exception)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
            }
        }
    }
}
