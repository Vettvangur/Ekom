using Ekom.Models;
using Ekom.Services;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Ekom.Controllers
{

    /// <summary>
    /// Handles order/cart creation, updates and removals
    /// </summary>
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

                if (checkoutResponse.HttpStatusCode == 300)
                {
                    return Redirect(checkoutResponse.ResponseBody as string);
                }

                Response.StatusCode = checkoutResponse.HttpStatusCode;
                return Content(checkoutResponse.ResponseBody as string);
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
        public async Task<ActionResult> Pay(PaymentRequest paymentRequest)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(paymentRequest.ReturnUrl) && Url.IsLocalUrl(paymentRequest.ReturnUrl))
                {
                    return Redirect(paymentRequest.ReturnUrl + "?errorStatus=badReturnUrl");
                }
                
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
                return Redirect(paymentRequest.ReturnUrl + "?errorStatus=serverError");
            }
        }

        private ActionResult ResponseHandler(CheckoutResponse checkoutResponse)
        {
            if (checkoutResponse != null)
            {
                if (checkoutResponse.HttpStatusCode != 530 ||
                    checkoutResponse.ResponseBody is not StockError stockError)
                {
                    if (checkoutResponse.HttpStatusCode == 230)
                    {
                        return Content(checkoutResponse.ResponseBody as string, "text/html");
                    }

                    if (checkoutResponse.HttpStatusCode == 400)
                    {
                        return Redirect(checkoutResponse.ReturnUrl + "?errorStatus=invalidData");
                    }

                    if (checkoutResponse.HttpStatusCode == 300)
                    {
                        return Redirect(checkoutResponse.ResponseBody as string);
                    }

                    return Redirect(
                        checkoutResponse.ReturnUrl + "?errorStatus=" + checkoutResponse.ResponseBody as string);
                }

                if (stockError.OrderLineKey == Guid.Empty)
                {
                    return Redirect(
                        checkoutResponse.ReturnUrl +
                        "?errorStatus=stockError&errorType=" +
                        stockError.Exception.Message);
                }
                else
                {
                    var type = stockError.IsVariant ? "variant" : "product";
                    return Redirect(checkoutResponse.ReturnUrl +
                                    $"?errorStatus=stockError&errorType={type}&orderline=" +
                                    stockError.OrderLineKey);
                }
            }
            else
            {
                return Redirect(checkoutResponse.ReturnUrl + "?success=true");
            }
        }
    }
}
