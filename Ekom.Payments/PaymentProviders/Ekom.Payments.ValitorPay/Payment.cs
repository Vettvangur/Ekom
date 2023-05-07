using Ekom.Payments.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Vettvangur.ValitorPay;
using Vettvangur.ValitorPay.Models;
using Vettvangur.ValitorPay.Models.Enums;

namespace Ekom.Payments.ValitorPay;

/// <summary>
/// Initiate a payment request with ValitorPay
/// </summary>
class Payment : IPaymentProvider
{
    internal const string _ppNodeName = "valitorPay";
    /// <summary>
    /// Ekom.Payments ResponseController
    /// </summary>
    const string initialPaymentReportPath = "/ekom/payments/valitorPay/completeFirstPayment";
    const string virtualCardReportPath = "/ekom/payments/valitorPay/completeVirtualCardPayment";

    readonly ILogger _logger;
    readonly PaymentsConfiguration _settings;
    readonly IUmbracoService _uService;
    readonly IOrderService _orderService;
    readonly HttpContext _httpCtx;
    readonly ValitorPayService _valitorPayService;
    readonly VirtualCardService _virtualCardService;

    /// <summary>
    /// ctor for Unit Tests
    /// </summary>
    public Payment(
        ILogger<Payment> logger,
        PaymentsConfiguration settings,
        IUmbracoService uService,
        IOrderService orderService,
        IHttpContextAccessor httpContext,
        ValitorPayService valitorPayService,
        VirtualCardService virtualCardService)
    {
        _logger = logger;
        _settings = settings;
        _uService = uService;
        _orderService = orderService;
        _httpCtx = httpContext.HttpContext ?? throw new NotSupportedException("Payment requests require an httpcontext");
        _valitorPayService = valitorPayService;
        _virtualCardService = virtualCardService;
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

            var valitorPaySettings = paymentSettings.CustomSettings.ContainsKey(typeof(ValitorPaySettings))
                ? paymentSettings.CustomSettings[typeof(ValitorPaySettings)] as ValitorPaySettings
                : new ValitorPaySettings();

            _uService.PopulatePaymentProviderProperties(
                paymentSettings,
                _ppNodeName,
                valitorPaySettings,
                ValitorPaySettings.Properties);

            var total = paymentSettings.Orders.Sum(x => x.GrandTotal);

            // Persist in database and retrieve unique order id
            var orderStatus = await _orderService.InsertAsync(
                total,
                paymentSettings,
                valitorPaySettings,
                null,
                _httpCtx
            ).ConfigureAwait(false);

            paymentSettings.SuccessUrl = PaymentsUriHelper.EnsureFullUri(paymentSettings.SuccessUrl, _httpCtx.Request);
            paymentSettings.SuccessUrl = PaymentsUriHelper.AddQueryString(paymentSettings.SuccessUrl, "?reference=" + orderStatus.UniqueId);
            paymentSettings.CancelUrl = PaymentsUriHelper.EnsureFullUri(paymentSettings.CancelUrl, _httpCtx.Request);
            paymentSettings.CancelUrl = PaymentsUriHelper.AddQueryString(paymentSettings.CancelUrl, "?reference=" + orderStatus.UniqueId);
            paymentSettings.ErrorUrl = PaymentsUriHelper.EnsureFullUri(paymentSettings.ErrorUrl, _httpCtx.Request);
            paymentSettings.ErrorUrl = PaymentsUriHelper.AddQueryString(paymentSettings.ErrorUrl, "?reference=" + orderStatus.UniqueId);

            _logger.LogInformation("ValitorPay Payment Request - Amount: " + total + " OrderId: " + orderStatus.UniqueId);

            if (string.IsNullOrEmpty(paymentSettings.VirtualCardNumber) && orderStatus.Member.HasValue)
            {
                var vCard = await _virtualCardService.GetMemberDefaultCardAsync(orderStatus.Member.Value);

                paymentSettings.VirtualCardNumber = vCard?.VirtualCardGuid.ToString();
            }

            if (string.IsNullOrEmpty(paymentSettings.VirtualCardNumber))
            {
                var reportUrl = PaymentsUriHelper.EnsureFullUri(
                new Uri(initialPaymentReportPath, UriKind.Relative),
                _httpCtx.Request);

                var retVal = await InitialCardUsageAsync(
                    paymentSettings,
                    valitorPaySettings,
                    orderStatus,
                    total,
                    reportUrl);

                if (!string.IsNullOrEmpty(retVal))
                {
                    return retVal;
                }
            }
            else
            {
                var reportUrl = PaymentsUriHelper.EnsureFullUri(
                    new Uri(virtualCardReportPath, UriKind.Relative),
                    _httpCtx.Request);

                var retVal = await SubsequentVirtualCardPayments(
                    paymentSettings,
                    valitorPaySettings,
                    orderStatus,
                    total,
                    reportUrl);

                if (!string.IsNullOrEmpty(retVal))
                {
                    return retVal;
                }
            }

            return paymentSettings.ErrorUrl.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ValitorPay Payment Request - Payment Request Failed");
            throw;
        }
    }

    private async Task<string> InitialCardUsageAsync(
        PaymentSettings paymentSettings,
        ValitorPaySettings valitorPaySettings,
        OrderStatus orderStatus,
        decimal grandTotal,
        Uri reportUrl)
    {
        var merchantData = new MerchantDataCard
        {
            OrderId = orderStatus.UniqueId.ToString(),

            CardNumber = paymentSettings.CardNumber,

            ExpirationMonth = paymentSettings.CardExpirationMonth,

            ExpirationYear = paymentSettings.CardExpirationYear,

            Cvc = paymentSettings.CardCVV,
        };

        var secret = valitorPaySettings.ApiKey
            .Split('.')
            .Last();

        var req = new CardVerificationRequest
        {
            CardNumber = paymentSettings.CardNumber,
            ExpirationMonth = paymentSettings.CardExpirationMonth,
            ExpirationYear = paymentSettings.CardExpirationYear,
            Amount = (long)(grandTotal * 100),
            AuthenticationUrl = reportUrl,
        };
        if (!string.IsNullOrEmpty(valitorPaySettings.TerminalId)
        && !string.IsNullOrEmpty(valitorPaySettings.AgreementNumber))
        {
            req.TerminalId = valitorPaySettings.TerminalId;
            req.AgreementNumber = valitorPaySettings.AgreementNumber;
        }

        req.SetMerchantData(merchantData, secret);

        var verificationResp = await _valitorPayService.CardVerificationAsync(req);
        if (verificationResp.IsSuccess)
        {
            return verificationResp.CardVerificationRawResponse;
        }
        else
        {
            return string.Empty;
        }
    }

    private async Task<string> SubsequentVirtualCardPayments(
        PaymentSettings paymentSettings,
        ValitorPaySettings valitorPaySettings,
        OrderStatus orderStatus,
        decimal grandTotal,
        Uri reportUrl)
    {
        var secret = valitorPaySettings.ApiKey
            .Split('.')
            .Last();

        var merchantData = new MerchantDataVirtualCard
        {
            OrderId = orderStatus.UniqueId.ToString(),

            VirtualCard = paymentSettings.VirtualCardNumber,
        };

        var req = new CardVerificationRequest
        {
            VirtualCard = paymentSettings.VirtualCardNumber,
            Amount = (long)(grandTotal * 100),
            AuthenticationUrl = reportUrl,
        };
        if (!string.IsNullOrEmpty(valitorPaySettings.TerminalId)
        && !string.IsNullOrEmpty(valitorPaySettings.AgreementNumber))
        {
            req.TerminalId = valitorPaySettings.TerminalId;
            req.AgreementNumber = valitorPaySettings.AgreementNumber;
        }

        req.SetMerchantData(merchantData, secret);

        var verificationResp = await _valitorPayService.CardVerificationAsync(req);
        if (verificationResp.IsSuccess)
        {
            return verificationResp.CardVerificationRawResponse;
        }
        else
        {
            return string.Empty;
        }
    }
}
