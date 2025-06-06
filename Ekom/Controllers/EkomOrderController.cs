using Ekom.API;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Net;

namespace Ekom.Controllers;


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
[ServiceFilter(typeof(ApiExceptionFilter))]
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
    public async Task<IActionResult> AddToOrder([FromBody] OrderRequest request)
    {
        if (request == null)
        {
            return BadRequest();
        }

        IOrderInfo orderInfo = await Order.Instance.AddOrderLineAsync(
            request.productId,
            request.quantity,
            request.storeAlias,
            new AddOrderSettings
            {
                OrderAction = request.action ?? OrderAction.AddOrUpdate,
                VariantKey = request.variantId
            });

        return Ok(orderInfo);

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
    /// Get order by id
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("{orderId}")]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        IOrderInfo order = await Order.Instance.GetOrderAsync(orderId);

        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    /// <summary>
    /// Get order by store
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("storeAlias/{storeAlias?}")]
    [Route("")]
    public async Task<IActionResult> GetOrder([FromRoute] string? storeAlias = null)
    {
        var order = await Order.Instance.GetOrderAsync(storeAlias);

        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    /// <summary>
    /// Get order by store
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("relatedproducts/storeAlias/{storeAlias?}/{count:Int}")]
    [Route("relatedproducts/{count:Int}")]
    public async Task<IActionResult> GetRelatedProducts([FromRoute] string? storeAlias = null, int count = 4)
    {
        var order = await Order.Instance.GetOrderAsync(storeAlias);

        if (order == null)
        {
            return NotFound();
        }

        return Ok(order.RelatedProducts(count));
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
    public async Task<IActionResult> UpdateCustomerInformation()
    {
        Microsoft.AspNetCore.Http.IFormCollection form = Request.Form;
        ICollection<string> keys = form.Keys;

        IOrderInfo orderInfo = await Order.Instance.UpdateCustomerInformationAsync(
            keys.ToDictionary(
                k => k,
                v => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(form[v])
        ));

        return Ok(orderInfo);
    }

    /// <summary>
    /// Update Shipping Information
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Route("update/shippingprovider/")]
    [Route("updateshippingprovider")]
    public async Task<IActionResult> UpdateShippingProvider()
    {
        Guid shippingProvider = Guid.Empty;
        string? storeAlias = null;
        Dictionary<string, string> customData = new();
        JObject? json = null;
        IFormCollection? form = null;

        // Read and parse form
        if (Request.HasFormContentType)
        {
            form = await Request.ReadFormAsync();

            // Get all form keys case-insensitively
            var formDict = form
                .ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            if (formDict.TryGetValue("ShippingProvider", out var formProviderStr) &&
                Guid.TryParse(formProviderStr, out var parsedFormGuid))
            {
                shippingProvider = parsedFormGuid;
            }

            formDict.TryGetValue("storeAlias", out storeAlias);

            customData = formDict
                .Where(x => x.Key.StartsWith("customshipping", StringComparison.OrdinalIgnoreCase) &&
                            !x.Key.Equals("ShippingProvider", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    x => x.Key,
                    x => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(x.Value)
                );
        }

        // Read and parse JSON
        else if (Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(body))
            {
                json = JObject.Parse(body);

                var jsonDict = json.Properties()
                    .ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);

                if (jsonDict.TryGetValue("ShippingProvider", out var jsonProviderStr) &&
                    Guid.TryParse(jsonProviderStr, out var parsedJsonGuid))
                {
                    shippingProvider = parsedJsonGuid;
                }

                jsonDict.TryGetValue("storeAlias", out storeAlias);

                customData = jsonDict
                    .Where(x => x.Key.StartsWith("customshipping", StringComparison.OrdinalIgnoreCase) &&
                                !x.Key.Equals("ShippingProvider", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(
                        x => x.Key,
                        x => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(x.Value)
                    );
            }
        }

        // Fallback: Query string (case-insensitive)
        var query = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        if (shippingProvider == Guid.Empty &&
            query.TryGetValue("ShippingProvider", out var queryProviderStr) &&
            Guid.TryParse(queryProviderStr, out var parsedQueryGuid))
        {
            shippingProvider = parsedQueryGuid;
        }

        if (string.IsNullOrWhiteSpace(storeAlias))
        {
            query.TryGetValue("storeAlias", out storeAlias);
        }

        if (shippingProvider == Guid.Empty || string.IsNullOrWhiteSpace(storeAlias))
            return BadRequest("Missing required ShippingProvider or storeAlias.");

        var orderInfo = await Order.Instance.UpdateShippingInformationAsync(shippingProvider, storeAlias, customData);
        return Ok(orderInfo);
    }

    /// <summary>
    /// Update Payment Information
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Route("update/paymentprovider/")]
    [Route("updatepaymentprovider")]
    public async Task<IActionResult> UpdatePaymentProvider()
    {
        Guid paymentProvider = Guid.Empty;
        string? storeAlias = null;
        Dictionary<string, string> customData = new();
        JObject? json = null;
        IFormCollection? form = null;

        if (Request.HasFormContentType)
        {
            form = await Request.ReadFormAsync();

            var formDict = form
                .ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            if (formDict.TryGetValue("PaymentProvider", out var formProviderStr) &&
                Guid.TryParse(formProviderStr, out var parsedFormGuid))
            {
                paymentProvider = parsedFormGuid;
            }

            formDict.TryGetValue("storeAlias", out storeAlias);

            customData = formDict
                .Where(x => x.Key.StartsWith("custompayment", StringComparison.OrdinalIgnoreCase) &&
                            !x.Key.Equals("PaymentProvider", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    x => x.Key,
                    x => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(x.Value)
                );
        }
        else if (Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(body))
            {
                json = JObject.Parse(body);

                var jsonDict = json.Properties()
                    .ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);

                if (jsonDict.TryGetValue("PaymentProvider", out var jsonProviderStr) &&
                    Guid.TryParse(jsonProviderStr, out var parsedJsonGuid))
                {
                    paymentProvider = parsedJsonGuid;
                }

                jsonDict.TryGetValue("storeAlias", out storeAlias);

                customData = jsonDict
                    .Where(x => x.Key.StartsWith("custompayment", StringComparison.OrdinalIgnoreCase) &&
                                !x.Key.Equals("PaymentProvider", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(
                        x => x.Key,
                        x => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(x.Value)
                    );
            }
        }

        // Fallback to query string (case-insensitive)
        var query = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        if (paymentProvider == Guid.Empty &&
            query.TryGetValue("PaymentProvider", out var queryProviderStr) &&
            Guid.TryParse(queryProviderStr, out var parsedQueryGuid))
        {
            paymentProvider = parsedQueryGuid;
        }

        if (string.IsNullOrWhiteSpace(storeAlias))
        {
            query.TryGetValue("storeAlias", out storeAlias);
        }

        if (paymentProvider == Guid.Empty || string.IsNullOrWhiteSpace(storeAlias))
            return BadRequest("Missing required PaymentProvider or storeAlias.");

        var orderInfo = await Order.Instance.UpdatePaymentInformationAsync(paymentProvider, storeAlias, customData);
        return Ok(orderInfo);
    }

    /// <summary>
    /// Remove product from order/cart
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("removeorderline")]
    public async Task<IActionResult> RemoveOrderLine([FromBody] OrderlineRequest model)
    {
        if (model == null)
        {
            return BadRequest();
        }

        if (string.IsNullOrEmpty(model.storeAlias))
        {
            return BadRequest("StoreAlias is missing");
        }

        IOrderInfo orderInfo = await Order.Instance.RemoveOrderLineAsync(model.lineId, model.storeAlias);

        return Ok(orderInfo);
    }


    /// <summary>
    /// Update quantity in orderline
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Route("Updateorderlinequantity")]
    public async Task<IActionResult> UpdateOrderLineQuantity([FromBody] OrderlineRequest model)
    {
        if (model == null)
        {
            return BadRequest();
        }

        if (string.IsNullOrEmpty(model.storeAlias))
        {
            return BadRequest("StoreAlias is missing");
        }


        IOrderInfo orderInfo = await Order.Instance.UpdateOrderlineQuantityAsync(model.lineId, model.quantity, model.storeAlias);

        return Ok(orderInfo);
    }

    /// <summary>
    /// Update currency for current store
    /// </summary>
    /// <param name="currency">Currency value</param>
    /// <returns></returns>
    [HttpPost]
    [Route("currency")]
    public async Task<IActionResult?> ChangeCurrency(string currency)
    {
        IStore? store = API.Store.Instance.GetStore();

        if (store == null)
        {
            return NotFound("Store not found.");
        }

        IOrderInfo? orderInfo = await Order.Instance.GetOrderAsync(store.Alias);

        if (orderInfo != null)
        {
            orderInfo = await Order.Instance.UpdateCurrencyAsync(currency, orderInfo.UniqueId, store.Alias).ConfigureAwait(false);
        }

        Response.Cookies.Append("EkomCurrency-" + store.Alias, currency, new Microsoft.AspNetCore.Http.CookieOptions
        {
            Expires = DateTime.UtcNow.AddDays(360),
        });

        return Ok(orderInfo);

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
    public async Task<IActionResult> ApplyCouponToOrder([FromBody] CouponRequest model)
    {
        if (string.IsNullOrEmpty(model.coupon))
        {
            return BadRequest("Coupon code can not be empty");
        }

        if (await Order.Instance.ApplyCouponToOrderAsync(model.coupon, model.storeAlias))
        {
            return Ok();
        }

        return StatusCode(450, "Discount not modified, better discount found");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Route("coupon/remove")]
    public async Task<IActionResult> RemoveCouponFromOrder(string storeAlias)
    {
        await Order.Instance.RemoveCouponFromOrderAsync(storeAlias);

        return Ok();
    }

    ///// <summary>
    ///// 
    ///// </summary>
    ///// <returns></returns>
    //[HttpPost]
    //[Route("coupon/orderline/apply")]
    //public async Task ApplyCouponToOrderLine(Guid productKey, string coupon, string storeAlias)
    //{
    //    try
    //    {
    //        if (await Order.Instance.ApplyCouponToOrderLineAsync(productKey, coupon, storeAlias))
    //        {
    //            throw new HttpResponseException(HttpStatusCode.OK);
    //        }
    //        else
    //        {
    //            throw new HttpResponseException(new HttpResponseMessage((HttpStatusCode)NoChangeResponse)
    //            {
    //                Content = new StringContent("Discount not modified, better discount found"),
    //            });
    //        }
    //    }
    //    catch (Exception ex) when (!(ex is HttpResponseException))
    //    {
    //        var r = ExceptionHandler.Handle<HttpResponseException>(ex);
    //        if (r != null)
    //        {
    //            throw r;
    //        }

    //        throw;
    //    }
    //}

    ///// <summary>
    ///// 
    ///// </summary>
    ///// <exception cref="OrderLineNotFoundException"></exception>
    ///// <exception cref="ArgumentException"></exception>
    //[HttpPost]
    //[Route("coupon/orderline/remove")]
    //public async Task RemoveCouponFromOrderLine(Guid productKey, string storeAlias)
    //{
    //    try
    //    {
    //        await Order.Instance.RemoveCouponFromOrderLineAsync(productKey, storeAlias);
    //        throw new HttpResponseException(HttpStatusCode.OK);
    //    }
    //    catch (Exception ex) when (!(ex is HttpResponseException))
    //    {
    //        var r = ExceptionHandler.Handle<HttpResponseException>(ex);
    //        if (r != null)
    //        {
    //            throw r;
    //        }

    //        throw;
    //    }
    //}
}
