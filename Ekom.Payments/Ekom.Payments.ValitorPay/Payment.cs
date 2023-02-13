using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Ekom.Payments.ValitorPay;

/// <summary>
/// Initiate a payment request with ValitorPay
/// </summary>
public class Payment : IPaymentProvider
{
    internal const string _ppNodeName = "valitorPay";
    /// <summary>
    /// Ekom.Payments ResponseController
    /// </summary>
    const string reportPath = "/umbraco/NetPayment/valitorpayresponse/post";

    readonly ILogger _logger;
    readonly PaymentsConfiguration _settings;
    readonly IUmbracoService _uService;
    readonly IOrderService _orderService;
    readonly HttpRequestBase _req;

    /// <summary>
    /// ctor for Unit Tests
    /// </summary>
    internal Payment(
        ILogger logger,
        PaymentsConfiguration settings,
        IUmbracoService uService,
        IOrderService orderService,
        HttpContextBase httpContext)
    {
        _logger = logger;
        _settings = settings;
        _uService = uService;
        _orderService = orderService;
        _req = httpContext.Request;
    }

    /// <summary>
    /// Initiate a payment request with ValitorPay.
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
        if (string.IsNullOrEmpty(paymentSettings.Store))
            throw new ArgumentException(nameof(paymentSettings.Store));

        try
        {
            _logger.LogInformation("ValitorPay Payment Request - Start");

            var valitorSettings = paymentSettings.CustomSettings.ContainsKey(typeof(ValitorPaySettings))
                ? paymentSettings.CustomSettings[typeof(ValitorPaySettings)] as ValitorPaySettings
                : new ValitorPaySettings();
            
            _uService.PopulatePaymentProviderProperties(
                paymentSettings, 
                _ppNodeName, 
                valitorSettings,
                ValitorPaySettings.Properties);
            
            _logger.LogDebug("ValitorPay Payment Request - Payment Provider Node: " + ppNode.Id);

            var total = paymentSettings.Orders.Sum(x => x.GrandTotal);

            var uProperties = _uService.BasePPProperties;
            uProperties.Add("paymentSuccessfulURLText", "");
            uProperties.Add("merchantName", "");
            if (paymentSettings.CheckoutTimeoutMinutes != 0)
            {
                uProperties.Add("timeoutRedirectURL", "");
            }
            uProperties = _uService.GetPPProperties(ppNode, paymentSettings.Store, uProperties);

            string merchantName = GetOverridingValue.Get(
                "merchantName",
                uProperties,
                null,
                paymentSettings.MerchantName);

            if (loanType != 0 && string.IsNullOrEmpty(merchantName))
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentException(
                    "ValitorPay Loans require MerchantName parameter from PaymentSettings or Umbraco node",
                    nameof(merchantName));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }

            var xmlConfig = _xmlSvc.GetConfigForPP(ppNode.Name, _ppNodeName);

            if (xmlConfig == null) throw new XmlConfigurationNotFoundException(ppNode.Name);

            var sb = new StringBuilder(xmlConfig["verificationcode"]);
            sb.Append("0");

            // Persist in database and retrieve unique order id
            var orderStatus  = await _orderService.InsertAsync(
                total,
                ppNode,
                paymentSettings,
                null,
                _req
            ).ConfigureAwait(false);

            if (string.IsNullOrEmpty(paymentSettings.ReportUrl))
            {
                paymentSettings.ReportUrl = URIHelper.EnsureFullUri(reportPath, _req);
            }
            else
            {
                paymentSettings.ReportUrl = URIHelper.EnsureFullUri(paymentSettings.ReportUrl, _req);
            }

            if (string.IsNullOrEmpty(paymentSettings.SuccessUrl))
            {
                // ValitorPay adds ReferenceNumber to querystring ?
                paymentSettings.SuccessUrl = URIHelper.EnsureFullUri(uProperties["successUrl"], _req);
            }
            else
            {
                paymentSettings.SuccessUrl = URIHelper.EnsureFullUri(paymentSettings.SuccessUrl, _req);
            }
            if (string.IsNullOrEmpty(paymentSettings.CancelUrl))
            {
                paymentSettings.CancelUrl = URIHelper.EnsureFullUri(uProperties["cancelUrl"], _req);
            }
            else
            {
                paymentSettings.CancelUrl = URIHelper.EnsureFullUri(paymentSettings.CancelUrl, _req);
            }

            var currency = xmlConfig.ContainsKey("currency")
                ? xmlConfig["currency"]
                : "ISK";

            // Begin populating form values to be submitted
            var formValues = new Dictionary<string, string>
            {
                { "MerchantID", xmlConfig["merchantid"] },
                { "AuthorizationOnly", "0" },

                { "ReferenceNumber", orderStatus.UniqueId.ToString() },

                { "Currency", currency },
                { "Language", paymentSettings.Language.ToUpper() },

                { "PaymentSuccessfulURL", paymentSettings.SuccessUrl },
                { "PaymentSuccessfulURLText", string.IsNullOrEmpty(uProperties["paymentSuccessfulURLText"])
                    ? "."
                    :  uProperties["paymentSuccessfulURLText"]},
                { "PaymentSuccessfulAutomaticRedirect", paymentSettings.SkipReceipt ? "1" : "0" },
                { "PaymentCancelledURL", paymentSettings.CancelUrl },
                { "PaymentSuccessfulServerSideURL", paymentSettings.ReportUrl },
            };

            if (paymentSettings.CheckoutTimeoutMinutes != 0
            && uProperties.ContainsKey("timeoutRedirectURL"))
            {
                formValues.Add("SessionExpiredTimeoutInSeconds", paymentSettings.CheckoutTimeoutMinutes.ToString());
                var redirectUrl = URIHelper.EnsureFullUri(uProperties["timeoutRedirectURL"], _req);
                formValues.Add("SessionExpiredRedirectURL", redirectUrl);
            }
            else if (paymentSettings.CheckoutTimeoutMinutes != 0)
            {
                _logger.Error<Payment>("Requested checkout timeout but could not find redirect url, please configure payment provider with 'timeoutRedirectURL' property");
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

            sb.Append(xmlConfig["merchantid"]);
            sb.Append(orderStatus.UniqueId.ToString());
            sb.Append(paymentSettings.SuccessUrl);
            sb.Append(paymentSettings.ReportUrl);
            sb.Append(currency);

            if (loanType != 0)
            {
                formValues.Add("IsCardLoan", "1");
                formValues.Add("MerchantName", merchantName);

                if (loanType == 1)
                {
                    formValues.Add("IsInterestFree", "0");
                    sb.Append(0);
                }
                else if (loanType == 2)
                {
                    formValues.Add("IsInterestFree", "1");
                    sb.Append(1);
                }
            }

            formValues.Add("DigitalSignature", CryptoHelpers.GetSHA256HexStringSum(sb.ToString()));

            if (paymentSettings.ValitorSettings != null && paymentSettings.ValitorSettings.Any())
            {
                foreach (var settings in paymentSettings.ValitorSettings)
                {
                    formValues.Add(settings.Key, settings.Value);
                }
            }

            _logger.Info<Payment>("ValitorPay Payment Request - Amount: " + total + " OrderId: " + orderStatus.UniqueId);

            return FormHelper.CreateRequest(formValues, xmlConfig["url"]);
        }
        catch (Exception ex)
        {
            _logger.Error<Payment>(ex, "ValitorPay Payment Request - Payment Request Failed");
            throw;
        }
    }
}
