using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ekom.Controllers;


/// <summary>
/// Handles order/cart creation, updates and removals
/// </summary>
[Route("ekom/checkout")]
[ServiceFilter(typeof(ApiExceptionFilter))]
public class EkomCheckoutApiController : ControllerBase
{
    /// <summary>
    /// ctor
    /// </summary>
    public EkomCheckoutApiController(ILogger<EkomCheckoutApiController> logger, CheckoutControllerService checkoutControllerService)
    {
        _logger = logger;
        _checkoutControllerService = checkoutControllerService;
    }
    private readonly ILogger _logger;
    private readonly CheckoutControllerService _checkoutControllerService;

    /// <summary>
    /// Complete payment using the Standard Ekom checkout controller
    /// </summary>
    [Route("pay")]
    [HttpPost]
    public async Task<IActionResult> Pay([FromQuery] string culture, CancellationToken ct = default)
    {
        try
        {
            Request.EnableBuffering();

            culture = string.IsNullOrEmpty(culture) ? Thread.CurrentThread.CurrentCulture.Name : culture;

            PaymentRequest? paymentRequest = null;
            Dictionary<string, string> additionalData = new(StringComparer.OrdinalIgnoreCase);

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(ct);

                paymentRequest = new PaymentRequest
                {
                    PaymentProvider = form.TryGetValue("PaymentProvider", out var pp) && Guid.TryParse(pp, out var ppGuid) ? ppGuid : null,
                    ShippingProvider = form.TryGetValue("ShippingProvider", out var sp) && Guid.TryParse(sp, out var spGuid) ? spGuid : null,
                    CardNumber = form.TryGetValue("CardNumber", out var card) ? card.ToString() : "",
                    CVV = form.TryGetValue("CVV", out var cvv) ? cvv.ToString() : "",
                    Year = form.TryGetValue("Year", out var yearStr) && int.TryParse(yearStr, out var year) ? year : null,
                    Month = form.TryGetValue("Month", out var monthStr) && int.TryParse(monthStr, out var month) ? month : null,
                    StoreAlias = form.TryGetValue("StoreAlias", out var storeAlias) ? storeAlias.ToString() : "",
                    ReturnUrl = form.TryGetValue("ReturnUrl", out var returnUrl) ? returnUrl.ToString() : "",
                    Culture = form.TryGetValue("Culture", out var cultureVal) ? cultureVal.ToString() : culture,
                    CspNonce = form.TryGetValue("Nonce", out var nonceValue) ? nonceValue.ToString() : null
                };

                var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "PaymentProvider", "ShippingProvider", "CardNumber", "CVV", "Year", "Month", "StoreAlias", "ReturnUrl", "Culture"
                };

                paymentRequest.AdditionalData = form
                    .Where(kvp => !knownKeys.Contains(kvp.Key))
                    .ToDictionary(
                        kvp => kvp.Key.Trim(),
                        kvp => kvp.Value.ToString().Trim()
                    );

            }
            else
            {
                // Expect JSON
                using var reader = new StreamReader(Request.Body);
                var jsonString = await reader.ReadToEndAsync();
                var jObject = JObject.Parse(jsonString);

                paymentRequest = jObject.ToObject<PaymentRequest>();

                if (paymentRequest == null)
                {
                    return BadRequest("Invalid payment request data.");
                }

                additionalData = jObject.Properties()
                    .DistinctBy(p => p.Name)
                    .ToDictionary(p => p.Name.Trim(), p => p.Value.ToString().Trim());
            }

            paymentRequest!.AdditionalData = additionalData;

            return await _checkoutControllerService.PayAsync(ResponseHandler, paymentRequest, culture, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkout payment failed!");
            throw;
        }
      
    }

    private IActionResult ResponseHandler(CheckoutResponse checkoutResponse)
    {
        if (checkoutResponse != null)
        {
            if (checkoutResponse.HttpStatusCode == 400)
            {
                return BadRequest();
            }

            if (checkoutResponse.HttpStatusCode == 300)
            {
                return Redirect(checkoutResponse.ResponseBody as string);
            }

            Response.StatusCode = checkoutResponse.HttpStatusCode;
            return Content(JsonConvert.SerializeObject(checkoutResponse.ResponseBody), "application/json");
        }

        return Ok();
    }
}

/// <summary>
/// Offers a default way to complete checkout using Ekom
/// </summary>
[Route("ekom/mvcCheckout")]
public class CheckoutController : ControllerBase
{
    /// <summary>
    /// ctor
    /// </summary>
    public CheckoutController(ILogger<CheckoutController> logger, CheckoutControllerService checkoutControllerService)
    {
        _logger = logger;
        _checkoutControllerService = checkoutControllerService;
    }
    private readonly ILogger _logger;
    private readonly CheckoutControllerService _checkoutControllerService;

    /// <summary>
    /// Complete payment using the Standard Ekom checkout controller
    /// </summary>
    /// <param name="paymentRequest"></param>
    /// <returns></returns>
    [Route("pay")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> Pay(PaymentRequest paymentRequest, CancellationToken ct = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(paymentRequest.ReturnUrl) && Url.IsLocalUrl(paymentRequest.ReturnUrl))
            {
                return Redirect(paymentRequest.ReturnUrl + "?errorStatus=badReturnUrl");
            }

            var form = Request.Form;

            Dictionary<string, string> additionalData = form
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(g => g.Key, g => g.First().Value.ToString());

            paymentRequest.AdditionalData = additionalData;

            string culture = string.IsNullOrEmpty(paymentRequest.Culture) ? Thread.CurrentThread.CurrentCulture.Name : paymentRequest.Culture;

            var payResponse = await _checkoutControllerService.PayAsync(
                ResponseHandler,
                paymentRequest,
                culture,
                ct);

            return payResponse;
        }
        catch (Exception ex)
        {

            _logger.LogError(ex, "Checkout payment failed!");

            var returnUrl = paymentRequest.ReturnUrl ?? "/";
            var finalUrl = QueryHelpers.AddQueryString(returnUrl, "errorStatus", "serverError");
            
            if (ex is EkomProblemDetailsException)
            {
                var problemException = ex as EkomProblemDetailsException;

                var title = problemException.ProblemDetails.Title;
                var detail = problemException.ProblemDetails.Detail;

                string problemMessage = !string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(detail)
                    ? $"{title} - {detail}"
                    : title ?? detail ?? string.Empty;

                var url = QueryHelpers.AddQueryString(returnUrl, "errorMessage", problemMessage);

                return ResponseHandler(new CheckoutResponse()
                {
                    HttpStatusCode = problemException.ProblemDetails.Status.HasValue ? problemException.ProblemDetails.Status.Value : 500,
                    ReturnUrl = url
                });
            }

            return Redirect(finalUrl);
        }
    }

    private ActionResult ResponseHandler(CheckoutResponse checkoutResponse)
    {
        if (checkoutResponse == null)
        {
            return StatusCode(500);
        }

        var returnUrl = checkoutResponse.ReturnUrl ?? "/";

        if (checkoutResponse.HttpStatusCode != 530 ||
            checkoutResponse.ResponseBody is not StockError stockError)
        {
            if (checkoutResponse.HttpStatusCode == 230)
            {
                return Content(checkoutResponse.ResponseBody as string, "text/html");
            }

            if (checkoutResponse.HttpStatusCode == 400)
            {
                var url = QueryHelpers.AddQueryString(returnUrl, "errorStatus", "invalidData");
                return Redirect(url);
            }

            if (checkoutResponse.HttpStatusCode == 300)
            {
                return Redirect(checkoutResponse.ResponseBody as string);
            }

            var error = checkoutResponse.ResponseBody?.ToString() ?? "servererror";
            var urlWithError = QueryHelpers.AddQueryString(returnUrl, "errorStatus", error);
            return Redirect(urlWithError);
        }

        if (stockError.OrderLineKey == Guid.Empty)
        {
            var url = QueryHelpers.AddQueryString(returnUrl, new Dictionary<string, string?>
            {
                { "errorStatus", "stockError" },
                { "errorType", stockError.Exception?.Message ?? "unknown" }
            });
            return Redirect(url);
        }
        else
        {
            var url = QueryHelpers.AddQueryString(returnUrl, new Dictionary<string, string?>
        {
            { "errorStatus", "stockError" },
            { "errorType", stockError.IsVariant ? "variant" : "product" },
            { "orderline", stockError.OrderLineKey.ToString() }
        });
            return Redirect(url);
        }
    }
}
