using Ekom.API;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Reflection;

namespace Ekom.Controllers
{

    /// <summary>
    /// Handles order/cart creation, updates and removals
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2007:Consider calling ConfigureAwait on the awaited task",
        Justification = "Async controller actions don't need ConfigureAwait")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "Async controller action")]

    [Route("ekom/order")]
    public partial class EkomOrderController : ControllerBase
    {
        /// <summary>
        /// ctor
        /// </summary>
        public EkomOrderController(ILogger<EkomOrderController> logger)
        {
            _logger = logger;
        }

        readonly ILogger _logger;

        /// <summary>
        /// Add product to order, also for updating or setting quantity of orderlines
        /// </summary>
        /// <param name="request">Guid Key of product</param>
        /// <returns></returns>
        [HttpPost]
        [Route("add")]
        public async Task<IOrderInfo> AddToOrder([FromBody] OrderRequest request)
        {
            if (request == null)
            {
                return null;
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
                    new AddOrderSettings
                    {
                        OrderAction = request.action ?? OrderAction.AddOrUpdate,
                        VariantKey = request.variantId,
                    });

                return orderInfo;
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                var r = ExceptionHandler.Handle<HttpResponseException>(ex);
                if (r != null)
                {
                    throw r;
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
        [HttpGet]
        [Route("storeAlias/{storeAlias}")]
        public async Task<IOrderInfo> GetOrder(string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            try
            {
                var order = await Order.Instance.GetOrderAsync(storeAlias);

                return order ?? throw new HttpResponseException(HttpStatusCode.NotFound);
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                var r = ExceptionHandler.Handle<HttpResponseException>(ex);
                if (r != null)
                {
                    throw r;
                }

                throw;
            }
        }

        /// <summary>
        /// Get order by store
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("relatedproducts/storeAlias/{storeAlias}/{count:Int}")]
        public async Task<IEnumerable<IProduct>> GetRelatedProducts(string storeAlias, int count = 4)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            try
            {
                var order = await Order.Instance.GetOrderAsync(storeAlias);

                if (order != null)
                {
                    return order.RelatedProducts(4);
                }

                throw new HttpResponseException(HttpStatusCode.NotFound);
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                var r = ExceptionHandler.Handle<HttpResponseException>(ex);
                if (r != null)
                {
                    throw r;
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
        [HttpPost]
        [Route("updatecustomer")]
        public async Task<IOrderInfo> UpdateCustomerInformation()
        {
            try
            {
                var form = Request.Form;
                var keys = form.Keys;

                var orderInfo = await Order.Instance.UpdateCustomerInformationAsync(
                    keys.ToDictionary(
                        k => k,
                        v => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(form[v])
                ));

                return orderInfo;
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                var r = ExceptionHandler.Handle<HttpResponseException>(ex);
                if (r != null)
                {
                    throw r;
                }

                throw;
            }
        }

        /// <summary>
        /// Update Shipping Information
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("update/shippingprovider/")]
        public async Task<IOrderInfo> UpdateShippingProvider(Guid ShippingProvider, string storeAlias)
        {
            try
            {
                var form = Request.Form;
                var keys = form.Keys;

                var orderInfo = await Order.Instance.UpdateShippingInformationAsync(ShippingProvider, storeAlias, keys.Where(x => x != "ShippingProvider" && x.StartsWith("shippingprovider", StringComparison.InvariantCulture)).ToDictionary(
                        k => k,
                        v => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(form[v])));

                return orderInfo;
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                var r = ExceptionHandler.Handle<HttpResponseException>(ex);
                if (r != null)
                {
                    throw r;
                }

                throw;
            }
        }

        /// <summary>
        /// Update Payment Information
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("update/paymentprovider/")]
        public async Task<IOrderInfo> UpdatePaymentProvider(Guid PaymentProvider, string storeAlias)
        {
            try
            {
                var form = Request.Form;
                var keys = form.Keys;

                var orderInfo = await Order.Instance.UpdatePaymentInformationAsync(PaymentProvider, storeAlias, keys.Where(x => x != "PaymentProvider" && x.StartsWith("paymentprovider", StringComparison.InvariantCulture)).ToDictionary(
                        k => k,
                        v => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(form[v])));

                return orderInfo;
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                var r = ExceptionHandler.Handle<HttpResponseException>(ex);
                if (r != null)
                {
                    throw r;
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
        [HttpPost]
        [Route("removeorderline")]
        public async Task<IOrderInfo> RemoveOrderLine([FromBody] OrderlineRequest model)
        {
            if (model == null)
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }
            
            if (string.IsNullOrEmpty(model.storeAlias))
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            try
            {
                var orderInfo = await Order.Instance.RemoveOrderLineAsync(model.lineId, model.storeAlias);

                return orderInfo;
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                var r = ExceptionHandler.Handle<HttpResponseException>(ex);
                if (r != null)
                {
                    throw r;
                }

                throw;
            }
        }


        /// <summary>
        /// Update quantity in orderline
        /// </summary>
        /// <param name="lineId">Guid Key of line</param>
        /// <param name="quantity">Quantity</param>
        /// <param name="storeAlias"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("Updateorderlinequantity")]
        public async Task<IOrderInfo> UpdateOrderLineQuantity([FromBody] OrderlineRequest model)
        {
            if (model == null)
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            if (string.IsNullOrEmpty(model.storeAlias))
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            try
            {
                var orderInfo = await Order.Instance.UpdateOrderlineQuantityAsync(model.lineId, model.quantity, model.storeAlias);

                return orderInfo;
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                var r = ExceptionHandler.Handle<HttpResponseException>(ex);
                if (r != null)
                {
                    throw r;
                }

                throw;
            }
        }

        /// <summary>
        /// Update currency for current store
        /// </summary>
        /// <param name="currency">Currency value</param>
        /// <returns></returns>
        [HttpPost]
        [Route("currency")]
        public async Task<object> ChangeCurrency(string currency)
        {
            var store = API.Store.Instance.GetStore();

            var orderInfo = await Order.Instance.GetOrderAsync(store.Alias);

            if (orderInfo != null)
            {
                orderInfo = await Order.Instance.UpdateCurrencyAsync(currency, orderInfo.UniqueId, store.Alias).ConfigureAwait(false);
            }

            // ToDo: Verify this works correctly
            Response.Cookies.Append("EkomCurrency-" + store.Alias, currency, new Microsoft.AspNetCore.Http.CookieOptions
            {
                Expires = DateTime.UtcNow.AddDays(360),
            });

            return orderInfo;

        }

        /// <summary>
        /// Http status code custom response
        /// </summary>
        public const int NoChangeResponse = 450;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("coupon/apply")]
        public async Task ApplyCouponToOrder([FromBody] CouponRequest model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.coupon))
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent("Coupon code can not be empty"),
                    };
                    throw new HttpResponseException(resp);
                }

                if (await Order.Instance.ApplyCouponToOrderAsync(model.coupon, model.storeAlias))
                {
                    throw new HttpResponseException(HttpStatusCode.OK);
                }

                throw new HttpResponseException(new HttpResponseMessage((HttpStatusCode)NoChangeResponse)
                {
                    Content = new StringContent("Discount not modified, better discount found"),
                });
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                var r = ExceptionHandler.Handle<HttpResponseException>(ex);
                if (r != null)
                {
                    throw r;
                }

                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("coupon/remove")]
        public async Task RemoveCouponFromOrder(string storeAlias)
        {
            try
            {
                await Order.Instance.RemoveCouponFromOrderAsync(storeAlias);
                throw new HttpResponseException(HttpStatusCode.OK);
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                var r = ExceptionHandler.Handle<HttpResponseException>(ex);
                if (r != null)
                {
                    throw r;
                }

                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("coupon/orderline/apply")]
        public async Task ApplyCouponToOrderLine(Guid productKey, string coupon, string storeAlias)
        {
            try
            {
                if (await Order.Instance.ApplyCouponToOrderLineAsync(productKey, coupon, storeAlias))
                {
                    throw new HttpResponseException(HttpStatusCode.OK);
                }
                else
                {
                    throw new HttpResponseException(new HttpResponseMessage((HttpStatusCode)NoChangeResponse)
                    {
                        Content = new StringContent("Discount not modified, better discount found"),
                    });
                }
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                var r = ExceptionHandler.Handle<HttpResponseException>(ex);
                if (r != null)
                {
                    throw r;
                }

                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        [HttpPost]
        [Route("coupon/orderline/remove")]
        public async Task RemoveCouponFromOrderLine(Guid productKey, string storeAlias)
        {
            try
            {
                await Order.Instance.RemoveCouponFromOrderLineAsync(productKey, storeAlias);
                throw new HttpResponseException(HttpStatusCode.OK);
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                var r = ExceptionHandler.Handle<HttpResponseException>(ex);
                if (r != null)
                {
                    throw r;
                }

                throw;
            }
        }
    }
}
