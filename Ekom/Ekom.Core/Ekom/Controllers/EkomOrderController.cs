using Ekom.API;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Ekom.Controllers;

/// <summary>
/// Handles order/cart creation, updates and removals
/// </summary>
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
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    [HttpPost]
    [Route("add")]
    [Consumes("application/json", "application/x-www-form-urlencoded", "multipart/form-data")]
    [EnableRateLimiting("order-add")]
    public async Task<IActionResult> AddToOrder([FromBody] OrderRequest request, CancellationToken ct = default)
    {
        if (request == null) return BadRequest();

        Request.EnableBuffering();

        Dictionary<string, string> customData = new(StringComparer.OrdinalIgnoreCase);

        var contentType = Request.ContentType ?? "";
        var isJson = contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
        var isForm = Request.HasFormContentType;

        if (!isJson && !isForm)
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);

        if (Request.ContentLength is > 64_000)
            return BadRequest();


        if (isForm)
        {
            var form = await Request.ReadFormAsync();
            customData = form.ToDictionary(
                k => k.Key,
                v => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(v.Value.ToString()),
                StringComparer.OrdinalIgnoreCase
            );
        }
        else // Json
        {
            Request.Body.Position = 0;
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync(ct);
            }
            Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                body = body.Trim();

                try
                {
                    // Strict: must be exactly one JSON value AND it must be an object
                    var token = JToken.Parse(body);
                    if (token is not JObject obj)
                    {
                        return BadRequest("Json is not an object");
                    }

                    customData = obj.Properties().ToDictionary(
                        p => p.Name,
                        p => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(p.Value?.ToString() ?? ""),
                        StringComparer.OrdinalIgnoreCase);

                    request.Consent ??= obj["consent"]?.ToObject<OrderConsent>();
                    request.Tracking ??= obj["tracking"]?.ToObject<OrderTracking>();
                    customData.Remove("consent");
                    customData.Remove("tracking");
                }
                catch (JsonReaderException ex)
                {
                    
                    return BadRequest("Invalid JSON");
                }
            }
        }

        IOrderInfo orderInfo = await Order.Instance.AddOrderLineAsync(
            request.ProductId,
            request.Quantity,
            request.StoreAlias,
            new AddOrderSettings
            {
                OrderAction = request.Action ?? OrderAction.AddOrUpdate,
                VariantKey = request.VariantId,
                CustomData = customData,
                Consent = request.Consent,
                Tracking = request.Tracking
            }, ct);

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
    public async Task<IActionResult> GetOrder(Guid orderId, CancellationToken ct = default)
    {
        var order = await Order.Instance.GetOrderAsync(orderId, ct);

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
    public async Task<IActionResult> GetOrder([FromRoute] string? storeAlias = null, CancellationToken ct = default)
    {
        var order = await Order.Instance.GetOrderAsync(storeAlias, ct);

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
    public async Task<IActionResult> GetRelatedProducts([FromRoute] string? storeAlias = null, int count = 4, CancellationToken ct = default)
    {
        var order = await Order.Instance.GetOrderAsync(storeAlias, ct);

        if (order == null)
        {
            return NotFound();
        }
        // todo async
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
    /// <returns></returns>
    [HttpPost]
    [Route("updatecustomer")]
    public async Task<IActionResult> UpdateCustomerInformation(CancellationToken ct = default)
    {
        Request.EnableBuffering();

        Dictionary<string, string> customerData = new(StringComparer.OrdinalIgnoreCase);
        OrderConsent? consent = null;
        OrderTracking? tracking = null;

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            customerData = form.ToDictionary(
                k => k.Key,
                v => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(v.Value.ToString()),
                StringComparer.OrdinalIgnoreCase
            );
        }
        else if (Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync(ct);

            if (!string.IsNullOrWhiteSpace(body))
            {
                var json = JObject.Parse(body);

                consent = json["consent"]?.ToObject<OrderConsent>();
                tracking = json["tracking"]?.ToObject<OrderTracking>();

                customerData = json.Properties()
                    .Where(p => !p.Name.Equals("consent", StringComparison.OrdinalIgnoreCase))
                    .Where(p => !p.Name.Equals("tracking", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(
                        p => p.Name,
                        p => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(p.Value?.ToString() ?? ""),
                        StringComparer.OrdinalIgnoreCase
                    );
            }
        }
        else
        {
            return BadRequest("Unsupported content type. Only application/json or form data is supported.");
        }

        var orderInfo = await Order.Instance.UpdateCustomerInformationAsync(customerData, new OrderSettings
        {
            Consent = consent,
            Tracking = tracking
        }, ct: ct);
        return Ok(orderInfo);
    }

    [HttpPost]
    [Route("updatetracking")]
    [Consumes("application/json")]
    public async Task<IActionResult> UpdateTracking([FromBody] OrderTrackingUpdateRequest request, CancellationToken ct = default)
    {
        if (request?.Tracking == null || string.IsNullOrWhiteSpace(request.StoreAlias))
        {
            return BadRequest("Missing storeAlias or tracking.");
        }

        var orderInfo = await Order.Instance.UpdateTrackingAsync(request.StoreAlias, request.Tracking, new API.OrderSettings
        {
            Consent = request.Consent
        }, ct: ct);
        return Ok(orderInfo);
    }

    /// <summary>
    /// Update Shipping Information
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Route("update/shippingprovider/")]
    [Route("updateshippingprovider")]
    public async Task<IActionResult> UpdateShippingProvider(CancellationToken ct = default)
    {
        Request.EnableBuffering();

        var (shippingProvider, storeAlias, allData) = await ParseRequestAsync("ShippingProvider", ct);

        if (shippingProvider == Guid.Empty || string.IsNullOrWhiteSpace(storeAlias))
            return BadRequest("Missing required ShippingProvider or storeAlias.");

        var orderInfo = await Order.Instance.UpdateShippingInformationAsync(shippingProvider, storeAlias, allData, ct: ct);
        return Ok(orderInfo);
    }

    /// <summary>
    /// Update Payment Information
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Route("update/paymentprovider/")]
    [Route("updatepaymentprovider")]
    public async Task<IActionResult> UpdatePaymentProvider(CancellationToken ct = default)
    {
        Request.EnableBuffering();

        var (paymentProvider, storeAlias, allData) = await ParseRequestAsync("PaymentProvider", ct);
        if (paymentProvider == Guid.Empty || string.IsNullOrWhiteSpace(storeAlias))
            return BadRequest("Missing required PaymentProvider or storeAlias.");

        var orderInfo = await Order.Instance.UpdatePaymentInformationAsync(paymentProvider, storeAlias, allData, ct: ct);
        return Ok(orderInfo);
    }

    private async Task<(Guid providerId, string? storeAlias, Dictionary<string, string> allData)> ParseRequestAsync(string providerKey, CancellationToken ct = default)
    {
        Guid providerId = Guid.Empty;
        string? storeAlias = null;
        Dictionary<string, string> allData = new(StringComparer.OrdinalIgnoreCase);

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            var formDict = form.ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            if (formDict.TryGetValue(providerKey, out var formProviderStr) &&
                Guid.TryParse(formProviderStr, out var parsedFormGuid))
            {
                providerId = parsedFormGuid;
            }

            formDict.TryGetValue("storeAlias", out storeAlias);

            allData = formDict.ToDictionary(
                x => x.Key,
                x => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(x.Value),
                StringComparer.OrdinalIgnoreCase
            );
        }
        else if (Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync(ct);

            if (!string.IsNullOrWhiteSpace(body))
            {
                var json = JObject.Parse(body);

                var jsonDict = json.Properties()
                    .ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);

                if (jsonDict.TryGetValue(providerKey, out var jsonProviderStr) &&
                    Guid.TryParse(jsonProviderStr, out var parsedJsonGuid))
                {
                    providerId = parsedJsonGuid;
                }

                jsonDict.TryGetValue("storeAlias", out storeAlias);

                allData = jsonDict.ToDictionary(
                    x => x.Key,
                    x => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(x.Value),
                    StringComparer.OrdinalIgnoreCase
                );
            }
        }

        var query = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        if (providerId == Guid.Empty &&
            query.TryGetValue(providerKey, out var queryProviderStr) &&
            Guid.TryParse(queryProviderStr, out var parsedQueryGuid))
        {
            providerId = parsedQueryGuid;
        }

        if (string.IsNullOrWhiteSpace(storeAlias))
        {
            query.TryGetValue("storeAlias", out storeAlias);
        }

        return (providerId, storeAlias, allData);
    }

    /// <summary>
    /// Remove product from order/cart
    /// </summary>
    /// <param name="model"></param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    [HttpPost]
    [Route("removeorderline")]
    public async Task<IActionResult> RemoveOrderLine([FromBody] OrderlineRequest model, CancellationToken ct = default)
    {
        if (model == null)
        {
            return BadRequest();
        }

        if (string.IsNullOrEmpty(model.storeAlias))
        {
            return BadRequest("StoreAlias is missing");
        }

        IOrderInfo orderInfo = await Order.Instance.RemoveOrderLineAsync(model.lineId, model.storeAlias, ct: ct);

        return Ok(orderInfo);
    }


    /// <summary>
    /// Update quantity in orderline
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Route("Updateorderlinequantity")]
    public async Task<IActionResult> UpdateOrderLineQuantity([FromBody] OrderlineRequest model, CancellationToken ct = default)
    {
        if (model == null)
        {
            return BadRequest();
        }

        if (string.IsNullOrEmpty(model.storeAlias))
        {
            return BadRequest("StoreAlias is missing");
        }


        IOrderInfo orderInfo = await Order.Instance.UpdateOrderlineQuantityAsync(model.lineId, model.quantity, model.storeAlias, ct: ct);

        return Ok(orderInfo);
    }

    /// <summary>
    /// Update currency for current store
    /// </summary>
    /// <param name="currency">Currency value</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    [HttpPost]
    [Route("currency")]
    public async Task<IActionResult?> ChangeCurrency(string currency, CancellationToken ct = default)
    {
        IStore? store = API.Store.Instance.GetStore();

        if (store == null)
        {
            return NotFound("Store not found.");
        }

        IOrderInfo? orderInfo = await Order.Instance.GetOrderAsync(store.Alias, ct);

        if (orderInfo != null)
        {
            orderInfo = await Order.Instance.UpdateCurrencyAsync(currency, orderInfo.UniqueId, store.Alias, ct).ConfigureAwait(false);
        }

        Response.Cookies.Append(
            "EkomCurrency-" + store.Alias,
            currency,
            new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(360),
                SameSite = SameSiteMode.Lax,
                Secure = HttpContext.Request.IsHttps,
                HttpOnly = false
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
    [EnableRateLimiting("order-coupon")]
    public async Task<IActionResult> ApplyCouponToOrder([FromBody] CouponRequest model, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(model.coupon))
        {
            return BadRequest("Coupon code can not be empty");
        }

        if (await Order.Instance.ApplyCouponToOrderAsync(model.coupon, model.storeAlias, ct))
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
    [EnableRateLimiting("order-coupon")]
    public async Task<IActionResult> RemoveCouponFromOrder(string storeAlias, CancellationToken ct = default)
    {
        await Order.Instance.RemoveCouponFromOrderAsync(storeAlias, null, ct);

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
