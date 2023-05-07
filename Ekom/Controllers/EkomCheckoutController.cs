using Ekom.Models;
using Ekom.Payments;
using Ekom.Services;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Diagnostics.CodeAnalysis;

namespace Ekom.Controllers
{

    /// <summary>
    /// Handles order/cart creation, updates and removals
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "Async controller action")]
    [Route("ekom/checkout")]
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
        public async Task<IActionResult> Pay(PaymentRequest paymentRequest, string culture = "en-US")
        {
            return await _checkoutControllerService.PayAsync(ResponseHandler, paymentRequest, culture);
        }

        private IActionResult ResponseHandler(CheckoutResponse checkoutResponse)
        {
            if (checkoutResponse != null)
            {
                if (checkoutResponse.HttpStatusCode == 400)
                {
                    return BadRequest();
                }
                else if (checkoutResponse.HttpStatusCode == 300)
                {
                    return Redirect(checkoutResponse.ResponseBody as string);
                }
                else
                {
                    Response.StatusCode = checkoutResponse.HttpStatusCode;
                    return Content(checkoutResponse.ResponseBody as string);
                }
            }
            else
            {
                return Ok();
            }
        }    
    }

    /// <summary>
    /// Offers a default way to complete checkout using Ekom
    /// </summary>
    [Route("ekom/mvcCheckout")]
    [SuppressMessage(
        "Style",
        "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "Async controller action")]
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
        [Route("pay", Name = "MvcPay")]
        [HttpPost]
        public async Task<ActionResult> Pay(PaymentRequest paymentRequest)
        {
            try
            {
                var culture = Thread.CurrentThread.CurrentCulture.Name;
                return await _checkoutControllerService.PayAsync(
                    ResponseHandler, 
                    paymentRequest, 
                    culture);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.LogError(ex, "Checkout payment failed!");
                return Redirect(Request.GetDisplayUrl() + "?errorStatus=serverError");
            }
        }

        private ActionResult ResponseHandler(CheckoutResponse checkoutResponse)
        {
            if (checkoutResponse != null)
            {
                if (checkoutResponse.HttpStatusCode == 530 && checkoutResponse.ResponseBody is StockError stockError)
                {
                    if (stockError.OrderLineKey == Guid.Empty)
                    {
                        return Redirect(
                            Request.GetDisplayUrl() + 
                            "?errorStatus=stockError&errorType=" + 
                            stockError.Exception.Message);
                    }
                    else
                    {
                        var type = stockError.IsVariant ? "variant" : "product";
                        return Redirect(
                            Request.GetDisplayUrl() + 
                            $"?errorStatus=stockError&errorType={type}&orderline=" + 
                            stockError.OrderLineKey);
                    }
                }
                else if (checkoutResponse.HttpStatusCode == 230)
                {
                    return Content(checkoutResponse.ResponseBody as string, "text/html");
                }
                else if (checkoutResponse.HttpStatusCode == 400)
                {
                    return Redirect(Request.GetDisplayUrl() + "?errorStatus=invalidData");
                }
                else if (checkoutResponse.HttpStatusCode == 300)
                {
                    return Redirect(checkoutResponse.ResponseBody as string);
                }
                else
                {
                    return Redirect(Request.GetDisplayUrl() + "?errorStatus=" + checkoutResponse.ResponseBody as string);
                }
            }
            else
            {
                return Redirect(Request.GetDisplayUrl() + "?success=true");
            }
        }
    }
}
