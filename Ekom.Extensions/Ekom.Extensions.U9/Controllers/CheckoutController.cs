using Ekom.Models;
using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Website.Controllers;

namespace Ekom.Extensions.Controllers
{
    /// <summary>
    /// Offers a default way to complete checkout using Ekom
    /// </summary>
    [PluginController("Ekom")]
    [SuppressMessage(
        "Reliability",
        "CA2007:Consider calling ConfigureAwait on the awaited task",
        Justification = "Async controller actions don't need ConfigureAwait")]
    [SuppressMessage(
        "Style",
        "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "Async controller action")]
    public class CheckoutApiController : UmbracoApiController
    {
        private readonly ILogger _logger;
        private readonly CheckoutControllerService _checkoutControllerService;

        public CheckoutApiController(
            CheckoutControllerService checkoutControllerService, 
            ILogger<CheckoutApiController> logger)
        {
            _checkoutControllerService = checkoutControllerService;
            _logger = logger;
        }

        /// <summary>
        /// Complete payment using the Standard Ekom checkout controller
        /// </summary>
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

    /// <summary>
    /// Offers a default way to complete checkout using Ekom
    /// </summary>
    [PluginController("Ekom")]
    [SuppressMessage(
        "Reliability",
        "CA2007:Consider calling ConfigureAwait on the awaited task",
        Justification = "Async controller actions don't need ConfigureAwait")]
    [SuppressMessage(
        "Style",
        "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "Async controller action")]
    public class CheckoutController : SurfaceController
    {
        private readonly CheckoutControllerService _checkoutControllerService;
        private readonly ILogger _logger;

        public CheckoutController(
            IUmbracoContextAccessor umbracoContextAccessor,
            IUmbracoDatabaseFactory databaseFactory,
            ServiceContext services,
            AppCaches appCaches,
            IProfilingLogger profilingLogger,
            IPublishedUrlProvider publishedUrlProvider,
            CheckoutControllerService checkoutControllerService,
            ILogger<CheckoutController> logger)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
        {
            _checkoutControllerService = checkoutControllerService;
            _logger = logger;
        }

        /// <summary>
        /// Complete payment using the Standard Ekom checkout controller
        /// </summary>
        /// <param name="paymentRequest"></param>
        /// <returns></returns>
        public virtual async Task<IActionResult> Pay(PaymentRequest paymentRequest)
        {
            try
            {
                var culture = Thread.CurrentThread.CurrentCulture.Name;
                return await _checkoutControllerService.PayAsync(ResponseHandler, paymentRequest, culture);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                
                _logger.LogError(ex, "Checkout payment failed!");
                return RedirectToCurrentUmbracoPage(
                    new QueryString("?errorStatus=serverError"));
            }
        }

        private IActionResult ResponseHandler(CheckoutResponse checkoutResponse)
        {
            if (checkoutResponse != null)
            {
                if (checkoutResponse.HttpStatusCode == 530 && checkoutResponse.ResponseBody is StockError stockError)
                {
                    if (stockError.OrderLineKey == Guid.Empty)
                    {
                        return RedirectToCurrentUmbracoPage(
                            new QueryString(
                                "?errorStatus=stockError&errorType=" + stockError.Exception.Message));
                    }
                    else
                    {
                        var type = stockError.IsVariant ? "variant" : "product";
                        return RedirectToCurrentUmbracoPage(
                            new QueryString(
                                $"?errorStatus=stockError&errorType={type}&orderline=" + stockError.OrderLineKey));
                    }
                }
                else if (checkoutResponse.HttpStatusCode == 230)
                {
                    return Content(checkoutResponse.ResponseBody as string);
                }
                else if (checkoutResponse.HttpStatusCode == 400)
                {
                    return RedirectToCurrentUmbracoPage(new QueryString("?errorStatus=invalidData"));
                }
                else if (checkoutResponse.HttpStatusCode == 300)
                {
                    return Redirect(checkoutResponse.ResponseBody as string);
                }
                else
                {
                    return RedirectToCurrentUmbracoPage(
                        new QueryString(
                            "?errorStatus=" + checkoutResponse.ResponseBody as string));
                }
            }
            else
            {
                return RedirectToCurrentUmbracoPage(
                    new QueryString("?success=true"));
            }
        }
    }
}
