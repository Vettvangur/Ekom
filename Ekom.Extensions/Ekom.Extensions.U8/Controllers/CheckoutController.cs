using Ekom.Models;
using Ekom.Services;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Web.Composing;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;

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
        /// <summary>
        /// This monstrosity was born since there seems to be no better way to override Transient scoped
        /// services registered with Umbraco/LightInject.
        /// </summary>
        private readonly Lazy<CheckoutControllerService> _checkoutControllerService
            = new Lazy<CheckoutControllerService>(
                () => Current.Factory.TryGetInstance<CheckoutControllerService>()
                ?? Current.Factory.CreateInstance<CheckoutControllerService>());

        /// <summary>
        /// Complete payment using the Standard Ekom checkout controller
        /// </summary>
        public virtual async Task<IHttpActionResult> Pay(PaymentRequest paymentRequest, string culture = "en-US")
        {
            try
            {
                return await _checkoutControllerService.Value.PayAsync(ResponseHandler, paymentRequest, culture);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // We can't depend on library consumers configuring exception handling for Web Api controllers
                Logger.Error<CheckoutApiController>(ex, "Error executing Pay action");
                throw;
            }
        }

        private IHttpActionResult ResponseHandler(CheckoutResponse checkoutResponse)
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
                    return Content((HttpStatusCode)checkoutResponse.HttpStatusCode, checkoutResponse.ResponseBody);
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
        /// <summary>
        /// This monstrosity was born since there seems to be no better way to override Transient scoped
        /// services registered with Umbraco/LightInject.
        /// </summary>
        private readonly Lazy<CheckoutControllerService> _checkoutControllerService
            = new Lazy<CheckoutControllerService>(
                () => Current.Factory.TryGetInstance<CheckoutControllerService>()
                ?? Current.Factory.CreateInstance<CheckoutControllerService>());

        /// <summary>
        /// Complete payment using the Standard Ekom checkout controller
        /// </summary>
        /// <param name="paymentRequest"></param>
        /// <returns></returns>
        public virtual async Task<ActionResult> Pay(PaymentRequest paymentRequest)
        {
            try
            {
                var culture = Thread.CurrentThread.CurrentCulture.Name;
                return await _checkoutControllerService.Value.PayAsync(ResponseHandler, paymentRequest, culture);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Logger.Error<CheckoutController>(ex, "Checkout payment failed!");
                return RedirectToCurrentUmbracoPage("?errorStatus=serverError");
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
                        return RedirectToCurrentUmbracoPage("?errorStatus=stockError&errorType=" + stockError.Exception.Message);
                    }
                    else
                    {
                        var type = stockError.IsVariant ? "variant" : "product";
                        return RedirectToCurrentUmbracoPage($"?errorStatus=stockError&errorType={type}&orderline=" + stockError.OrderLineKey);
                    }
                }
                else if (checkoutResponse.HttpStatusCode == 230)
                {
                    return Content(checkoutResponse.ResponseBody as string);
                }
                else if (checkoutResponse.HttpStatusCode == 400)
                {
                    return RedirectToCurrentUmbracoPage("?errorStatus=invalidData");
                }
                else if (checkoutResponse.HttpStatusCode == 300)
                {
                    return Redirect(checkoutResponse.ResponseBody as string);
                }
                else
                {
                    return RedirectToCurrentUmbracoPage("?errorStatus=" + checkoutResponse.ResponseBody as string);
                }
            }
            else
            {
                return RedirectToCurrentUmbracoPage("?success=true");
            }
        }
    }
}
