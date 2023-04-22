using Ekom.Payments;
using Ekom.Payments.Helpers;
using Ekom.Payments.Valitor;
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

namespace Ekom.Payments.Valitor;

/// <summary>
/// Initiate a payment request with Valitor
/// </summary>
class Payment : IPaymentProvider
{
    internal const string _ppNodeName = "valitor";
    /// <summary>
    /// Ekom.Payments ResponseController
    /// </summary>
    const string reportPath = "/ekom/payments/valitorresponse";

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
    /// Initiate a payment request with Valitor.
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
            _logger.LogInformation("Valitor Payment Request - Start");

            var valitorSettings = paymentSettings.CustomSettings.ContainsKey(typeof(ValitorSettings))
                ? paymentSettings.CustomSettings[typeof(ValitorSettings)] as ValitorSettings
                : new ValitorSettings();

            _uService.PopulatePaymentProviderProperties(
                paymentSettings,
                _ppNodeName,
                valitorSettings,
                ValitorSettings.Properties);
            
            var total = paymentSettings.Orders.Sum(x => x.GrandTotal);
            if (valitorSettings.LoanType != 0 && total < 30000)
            {
                throw new ValitorAHKException("Requested loan amount will likely not meet icelandic ÁHK requirements");
            }

            if (valitorSettings.LoanType != 0 && string.IsNullOrEmpty(valitorSettings.MerchantName))
            {
                throw new NotSupportedException(
                    "Valitor Loans require MerchantName parameter");
            }

            var sb = new StringBuilder(valitorSettings.VerificationCode);
            sb.Append("0");

            // Persist in database and retrieve unique order id
            var orderStatus  = await _orderService.InsertAsync(
                total,
                paymentSettings,
                valitorSettings,
                null,
                _httpCtx
            ).ConfigureAwait(false);

            paymentSettings.SuccessUrl = PaymentsUriHelper.EnsureFullUri(paymentSettings.SuccessUrl, _httpCtx.Request);
            paymentSettings.SuccessUrl = PaymentsUriHelper.AddQueryString(paymentSettings.SuccessUrl, "?reference=" + orderStatus.UniqueId);

            var cancelUrl = PaymentsUriHelper.EnsureFullUri(paymentSettings.CancelUrl, _httpCtx.Request);
            var reportUrl = PaymentsUriHelper.EnsureFullUri(new Uri(reportPath, UriKind.Relative), _httpCtx.Request);

            // Begin populating form values to be submitted
            var formValues = new Dictionary<string, string?>
            {
                { "MerchantID", valitorSettings.MerchantId },
                { "AuthorizationOnly", "0" },

                { "ReferenceNumber", orderStatus.UniqueId.ToString() },

                { "Currency", paymentSettings.Currency },
                { "Language", ParseSupportedLanguages(paymentSettings.Language) },

                { "PaymentSuccessfulURL", paymentSettings.SuccessUrl.ToString() },
                { "PaymentSuccessfulURLText", /*valitorSettings.SkipReceipt*/ true 
                    ? "-" : 
                    valitorSettings.PaymentSuccessfulURLText 
                },
                { "PaymentSuccessfulAutomaticRedirect", /*valitorSettings.SkipReceipt*/ true 
                    ? "1" 
                    : "0" 
                },
                { "PaymentCancelledURL", cancelUrl.ToString() },

                { "PaymentSuccessfulServerSideURL", reportUrl.ToString() },
            };

            if (valitorSettings.SessionExpiredTimeoutInSeconds != 0
            && valitorSettings.SessionExpiredRedirectURL != null)
            {
                formValues.Add("SessionExpiredTimeoutInSeconds", valitorSettings.SessionExpiredTimeoutInSeconds.ToString());
                formValues.Add("SessionExpiredRedirectURL", valitorSettings.SessionExpiredRedirectURL.ToString());
            }
            else if (valitorSettings.SessionExpiredTimeoutInSeconds != 0)
            {
                _logger.LogError("Requested session expired timeout but could not find redirect url, please configure payment provider with 'timeoutRedirectURL' property");
            }

            for (int x = 0, length = paymentSettings.Orders.Count(); x < length; x++)
            {
                var order = paymentSettings.Orders.ElementAt(x);

                var lineNumber = x + 1;

                formValues.Add($"Product_{lineNumber}_Description",
                    HttpUtility.UrlEncode(order.Title, Encoding.GetEncoding("ISO-8859-1")));
                formValues.Add($"Product_{lineNumber}_Quantity", order.Quantity.ToString());
                formValues.Add($"Product_{lineNumber}_Price",  ((int)order.Price).ToString());
                formValues.Add($"Product_{lineNumber}_Discount", order.Discount.ToString());

                sb.Append(order.Quantity.ToString());
                sb.Append(((int)order.Price).ToString());
                sb.Append(order.Discount.ToString());
            }

            sb.Append(valitorSettings.MerchantId);
            sb.Append(orderStatus.UniqueId);
            sb.Append(paymentSettings.SuccessUrl);
            sb.Append(reportUrl);
            sb.Append(paymentSettings.Currency);

            if (valitorSettings.LoanType != LoanType.Disabled)
            {
                formValues.Add("IsCardLoan", "1");
                formValues.Add("MerchantName", valitorSettings.MerchantName);

                if (valitorSettings.LoanType == LoanType.IsLoan)
                {
                    formValues.Add("IsInterestFree", "0");
                    sb.Append(0);
                }
                else if (valitorSettings.LoanType == LoanType.IsInterestFreeLoan)
                {
                    formValues.Add("IsInterestFree", "1");
                    sb.Append(1);
                }
            }

            formValues.Add("DigitalSignature", CryptoHelpers.GetSHA256HexStringSum(sb.ToString()));

            _logger.LogInformation("Valitor Payment Request - Amount: " + total + " OrderId: " + orderStatus.UniqueId);

            return FormHelper.CreateRequest(formValues, valitorSettings.PaymentPageUrl.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Valitor Payment Request - Payment Request Failed");
            throw;
        }
    }

    public string ParseSupportedLanguages(string language)
    {
        var parsed 
            = CultureInfo.GetCultureInfo(language).TwoLetterISOLanguageName.ToUpper();

        switch (parsed)
        {
            case "IS":
            case "EN":
            case "DA":
            case "DE":
                return parsed;

            default:
                return "IS";
        }
    }
}
