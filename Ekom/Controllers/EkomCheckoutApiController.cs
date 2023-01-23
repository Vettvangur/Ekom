using Ekom.Models;
using Ekom.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
#if NETFRAMEWORK
    public partial class EkomCheckoutApiController : ApiController
    {
        /// <summary>
        /// ctor
        /// </summary>
        public EkomCheckoutApiController()
        {
            _logger = Ekom.Configuration.Resolver.GetService<ILogger<EkomOrderController>>();
            _checkoutControllerService = Ekom.Configuration.Resolver.GetService<CheckoutControllerService>();
        }
#else
    [Route("ekom/checkout")]
    public partial class EkomCheckoutApiController : ControllerBase
    {
        /// <summary>
        /// ctor
        /// </summary>
        public EkomCheckoutApiController(ILogger<EkomOrderController> logger, CheckoutControllerService checkoutControllerService)
        {
            _logger = logger;
            _checkoutControllerService = checkoutControllerService;
        }
#endif

        private readonly ILogger _logger;
        private readonly CheckoutControllerService _checkoutControllerService;

        /// <summary>
        /// Complete payment using the Standard Ekom checkout controller
        /// </summary>
        [Route("pay")]
        [HttpPost]
        public virtual async Task<IActionResult> Pay(PaymentRequest paymentRequest, string culture = "en-US")
        {
            try
            {
                return await _checkoutControllerService.PayAsync(ResponseHandler, paymentRequest, culture);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // We can't depend on library consumers configuring exception handling for Web Api controllers
                _logger.LogError(ex, "Error executing Pay action");
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
}
