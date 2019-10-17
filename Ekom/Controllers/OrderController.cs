using Ekom.API;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Umbraco.Core.Logging;
using Umbraco.Web.Mvc;

namespace Ekom.Controllers
{
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

    /// <summary>
    /// Handles order/cart creation, updates and removals
    /// </summary>
    [PluginController("Ekom")]
    public partial class OrderController : SurfaceController
    {
        readonly ILogger _logger;
        /// <summary>
        /// ctor
        /// </summary>
        public OrderController(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Add product to order, also for updating or setting quantity of orderlines
        /// </summary>
        /// <param name="request">Guid Key of product</param>
        /// <returns></returns>
        public async Task<ActionResult> AddToOrder(OrderRequest request)
        {
            if (request == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            try
            {
                //var variantIds = new List<Guid>();

                //if (request.variantId != null && request.variantId != Guid.Empty)
                //{
                //    variantIds.Add(request.variantId.Value);
                //}

                var orderInfo = await Order.Instance.AddOrderLineAsync(
                    request.productId,
                    request.quantity,
                    request.storeAlias,
                    request.action,
                    request.variantId);

                return Json(new
                {
                    success = true,
                    orderInfo
                });
            }
            catch (Exception ex)
            {
                var r = ExceptionHandler.Handle<HttpStatusCodeResult>(ex);
                if (r != null)
                {
                    return r;
                }

                throw;
            }
        }

        ///// <summary>
        ///// Get order
        ///// </summary>
        ///// <returns></returns>
        //public ActionResult GetOrder()
        //{
        //    var order = Order.Instance.GetOrder();

        //    return Json(order, JsonRequestBehavior.AllowGet);
        //}

        /// <summary>
        /// Get order by store
        /// </summary>
        /// <returns></returns>
        public ActionResult GetOrder(string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            try
            {
                var order = Order.Instance.GetOrder(storeAlias);

                if (order == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.NotFound);
                }

                return Json(order ?? new object(), JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                var r = ExceptionHandler.Handle<HttpStatusCodeResult>(ex);
                if (r != null)
                {
                    return r;
                }

                throw;
            }
        }

        ///// <summary>
        ///// Add product with multiple variants to order
        ///// </summary>
        ///// <param name="request">OrderMultipleRequest</param>
        ///// <returns></returns>
        //public JsonResult AddMultipleToOrder(OrderMultipleRequest request)
        //{
        //    try
        //    {
        //        var os = Order.Instance;

        //        _log.Info("Add To Multiple Order: " + request.productId);

        //        if (request.variant.Any())
        //        {
        //            IOrderInfo o = null;

        //            var variantIds = new List<Guid>();

        //            foreach (var variant in request.variant)
        //            {
        //                _log.Info("Add To Multiple Order: Variant: " + variant.Id + " - " + variant.Quantity);

        //                if (Guid.TryParse(variant.Id.ToString(), out Guid variantId))
        //                {
        //                    var items = new List<Guid>
        //                    {
        //                        variantId
        //                    };

        //                    o = os.AddOrderLine(request.productId, items, variant.Quantity, request.storeAlias, request.action);

        //                }
        //            }

        //            return Json(new
        //            {
        //                success = true,
        //                orderInfo = o
        //            });

        //        }

        //        return Json(new
        //        {
        //            success = false
        //        });

        //    }

        //    catch (Exception ex)
        //    {
        //        _log.Error("Add to multiple order Failed!", ex);

        //        return Json(new
        //        {
        //            success = false,
        //            error = ex.Message
        //        });
        //    }
        //}

        /// <summary>
        /// Update Customer Information to Order
        /// </summary>
        /// <param name="form">FormData</param>
        /// <returns></returns>
        public async Task<ActionResult> UpdateCustomerInformation(FormCollection form)
        {
            try
            {
                var orderInfo = await Order.Instance.UpdateCustomerInformationAsync(
                    form.AllKeys.ToDictionary(k => k, v => form[v])
                );

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });
            }
            catch (Exception ex)
            {
                var r = ExceptionHandler.Handle<HttpStatusCodeResult>(ex);
                if (r != null)
                {
                    return r;
                }

                throw;
            }
        }

        /// <summary>
        /// Update Shipping Information
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> UpdateShippingProvider(Guid ShippingProvider, string storeAlias)
        {
            try
            {
                var orderInfo = await Order.Instance.UpdateShippingInformationAsync(ShippingProvider, storeAlias);

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });
            }
            catch (Exception ex)
            {
                var r = ExceptionHandler.Handle<HttpStatusCodeResult>(ex);
                if (r != null)
                {
                    return r;
                }

                throw;
            }
        }

        /// <summary>
        /// Update Payment Information
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> UpdatePaymentProvider(Guid PaymentProvider, string storeAlias)
        {
            try
            {
                var orderInfo = await Order.Instance.UpdatePaymentInformationAsync(PaymentProvider, storeAlias);

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });
            }
            catch (Exception ex)
            {
                var r = ExceptionHandler.Handle<HttpStatusCodeResult>(ex);
                if (r != null)
                {
                    return r;
                }

                throw;
            }
        }

        /// <summary>
        /// Update order, change quantity of line in cart/order
        /// </summary>
        /// <param name="lineId">Guid Key of line to update</param>
        /// <param name="storeAlias"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        [Obsolete("Deprecated, use AddToOrder and specify OrderAction")]
        public async Task<ActionResult> UpdateOrder(Guid lineId, string storeAlias, int quantity)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            if (quantity == 0)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            try
            {
                var orderInfo = await Order.Instance.AddOrderLineAsync(
                    lineId,
                    quantity,
                    storeAlias);

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });
            }
            catch (Exception ex)
            {
                var r = ExceptionHandler.Handle<HttpStatusCodeResult>(ex);
                if (r != null)
                {
                    return r;
                }

                throw;
            }
        }

        /// <summary>
        /// Remove product from order/cart
        /// </summary>
        /// <param name="lineId">Guid Key of product/line</param>
        /// <param name="storeAlias"></param>
        /// <returns></returns>
        public async Task<ActionResult> RemoveOrderLine(Guid lineId, string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            try
            {
                var orderInfo = await Order.Instance.RemoveOrderLineAsync(lineId, storeAlias);

                return Json(new { orderInfo, date = DateTime.Now });
            }
            catch (Exception ex)
            {
                var r = ExceptionHandler.Handle<HttpStatusCodeResult>(ex);
                if (r != null)
                {
                    return r;
                }

                throw;
            }
        }
    }
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
}
