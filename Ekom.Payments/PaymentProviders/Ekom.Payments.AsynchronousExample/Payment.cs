using Ekom.Payments;
using Ekom.Payments.Helpers;
using Ekom.Payments.AsynchronousExample;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Ekom.Payments.AsynchronousExample;

/// <summary>
/// Initiate a payment request with AsynchronousExample
/// </summary>
class Payment : IPaymentProvider
{
    internal const string _ppNodeName = "AsynchronousExample";
    /// <summary>
    /// Ekom.Payments ResponseController
    /// </summary>
    const string reportPath = "/ekom/payments/AsynchronousExampleresponse";

    readonly ILogger<Payment> _logger;
    readonly PaymentsConfiguration _settings;
    readonly IUmbracoService _uService;
    readonly IOrderService _orderService;
    readonly HttpContext _httpCtx;

    /// <summary>
    /// ctor for Unit Tests
    /// </summary>
    public Payment(
        ILogger<Payment> logger,
        PaymentsConfiguration settings,
        IUmbracoService uService,
        IOrderService orderService,
        IHttpContextAccessor httpContext)
    {
        _logger = logger;
        _settings = settings;
        _uService = uService;
        _orderService = orderService;
        _httpCtx = httpContext.HttpContext ?? throw new NotSupportedException("Payment requests require an httpcontext");
    }

    /// <summary>
    /// Initiate a payment request with AsynchronousExample.
    /// When calling RequestAsync, always await the result.
    /// </summary>
    /// <param name="paymentSettings">Configuration object for PaymentProviders</param>
    public async Task<string> RequestAsync(PaymentSettings paymentSettings)
    {
        if (paymentSettings == null)
            throw new ArgumentNullException(nameof(paymentSettings));
        if (paymentSettings.Orders == null)
            throw new ArgumentNullException(nameof(paymentSettings.Orders));
        if (string.IsNullOrEmpty(paymentSettings.Language))
            throw new ArgumentException(nameof(paymentSettings.Language));

        try
        {
            _logger.LogInformation("AsynchronousExample Payment Request - Start");

            var asynchronousExampleSettings = paymentSettings.CustomSettings.ContainsKey(typeof(AsynchronousExampleSettings))
                ? (paymentSettings.CustomSettings[typeof(AsynchronousExampleSettings)] as AsynchronousExampleSettings)!
                : new AsynchronousExampleSettings();

            _uService.PopulatePaymentProviderProperties(
                paymentSettings,
                _ppNodeName,
                asynchronousExampleSettings,
                AsynchronousExampleSettings.Properties);
            
            var total = paymentSettings.Orders.Sum(x => x.GrandTotal);

            // Persist in database and retrieve unique order id
            var orderStatus  = await _orderService.InsertAsync(
                total,
                paymentSettings,
                asynchronousExampleSettings,
                null,
                _httpCtx
            ).ConfigureAwait(false);

            // This example shows how to construct a success url / receipt page destination
            // adding the reference querystring allows for easy retrieval of order using the order helper on the success/receipt page
            paymentSettings.SuccessUrl = PaymentsUriHelper.EnsureFullUri(paymentSettings.SuccessUrl, _httpCtx.Request);
            paymentSettings.SuccessUrl = PaymentsUriHelper.AddQueryString(paymentSettings.SuccessUrl, "?reference=" + orderStatus.UniqueId);

            var cancelUrl = PaymentsUriHelper.EnsureFullUri(paymentSettings.CancelUrl, _httpCtx.Request);
            var callbackUrl = PaymentsUriHelper.EnsureFullUri(new Uri(reportPath, UriKind.Relative), _httpCtx.Request);


            _logger.LogInformation("AsynchronousExample Payment Request - Amount: {Total} OrderId: {OrderUniqueId}", total, orderStatus.UniqueId);

            // If your payment provider expects a client initiated post request to send the user to it's payment portal
            // the following form helper is useful.
            var formValues = new Dictionary<string, string?>
            {

            };
            return FormHelper.CreateRequest(formValues, asynchronousExampleSettings.PaymentPageUrl.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AsynchronousExample Payment Request - Payment Request Failed");
            throw;
        }
    }
}
