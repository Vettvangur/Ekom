using Ekom.Payments;
using Ekom.Payments.Helpers;
using Ekom.Payments.ValitorPay;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Vettvangur.ValitorPay;
using Vettvangur.ValitorPay.Models;
using Vettvangur.ValitorPay.Models.Enums;

namespace Ekom.Payments.ValitorPay;

/// <summary>
/// Receives a callback from Valitor when customer completes payment.
/// Changes order status and optionally runs a custom callback provided by the application consuming this library.
/// </summary>
[Route("ekom/payments/[controller]")]
[ApiController]
public class ValitorPayController : ControllerBase
{
    readonly ILogger _logger;
    readonly IConfiguration _config;
    readonly PaymentsConfiguration _settings;
    readonly IOrderService _orderService;
    readonly IDatabaseFactory _dbFac;
    readonly IMailService _mailSvc;
    readonly ValitorPayService _valitorPayService;

    /// <summary>
    /// ctor
    /// </summary>
    public ValitorPayController(
        ILogger<ValitorPayController> logger,
        IConfiguration config,
        PaymentsConfiguration settings,
        IOrderService orderService,
        IDatabaseFactory dbFac,
        IMailService mailSvc,
        ValitorPayService valitorPayService)
    {
        _logger = logger;
        _config = config;
        _settings = settings;
        _orderService = orderService;
        _dbFac = dbFac;
        _mailSvc = mailSvc;
        _valitorPayService = valitorPayService;
    }

    /// <summary>
    /// <param name="notificationCallBack"></param>
    /// </summary>
    /// <returns></returns>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("")]
    [HttpPost]
    public async Task<IActionResult> CompleteVirtualCardPayment(CardVerificationCallback notificationCallBack)
    {
        _logger.LogInformation("CompleteVirtualCardPayment");

        var response = new HttpResponseMessage { };

        try
        {
            var apiKey = _config["Ekom:Payments:ValitorPay:ApiKey"];
            var secret = apiKey
                .Split('.')
                .Last();
            var md = notificationCallBack.GetMerchantData<MerchantDataVirtualCard>(secret);

            _logger.LogInformation("Valitor Payment Response - OrderID: " + md.OrderId);

            OrderStatus? order = await _orderService.GetAsync(Guid.Parse(md.OrderId));

            if (order == null)
            {
                _logger.LogWarning("No order found for id: " + md.OrderId);
                return NotFound();
            }

            if (order.Paid)
            {
                _logger.LogWarning("Order already paid: " + md.OrderId);
                return Ok();
            }
            
            _logger.LogInformation("CompleteVirtualCardPayment - order.ID: " + order?.ReferenceId);

            order.DsTransID = notificationCallBack.TDS2.DsTransID;
            order.Updated = DateTime.Now;

            // Remove encrypted card information before logging
            notificationCallBack.MD = null;
            _logger.LogDebug(JsonConvert.SerializeObject(notificationCallBack));

            if (!MDStatusCodes.CodeMap.ContainsKey(notificationCallBack.MdStatus))
            {
                throw new NotSupportedException("Unknown MdStatus code");
            }

            var paymentSettings = JsonConvert.DeserializeObject<PaymentSettings>(order.EkomPaymentSettingsData);
            var valitorSettings = JsonConvert.DeserializeObject<ValitorPaySettings>(order.EkomPaymentProviderData);

            _valitorPayService.ConfigureAgreement(valitorSettings.AgreementNumber, valitorSettings.TerminalId);

            var query = Request.Query;

            if (MDStatusCodes.CodeMap[notificationCallBack.MdStatus].Type == CodeType.Success
            && VerifySignature(md, notificationCallBack.Signature))
            {
                _logger.LogInformation($"CompleteVirtualCardPayment - order.ID: {order.UniqueId} - MD Status Success");

                var resp = await _valitorPayService.VirtualCardPaymentAsync(new VirtualCardPaymentRequest
                {
                    Operation = Operation.Sale,
                    VirtualCardNumber = md.VirtualCard,
                    CardVerificationData = new CardVerificationData
                    {
                        DsTransId = notificationCallBack.TDS2.DsTransID,
                        Cavv = notificationCallBack.Cavv,
                        Xid = notificationCallBack.Xid,
                        MdStatus = notificationCallBack.MdStatus,
                    },
                    Amount = (long)(order.Amount * 100),
                    VirtualCardPaymentAdditionalData = new AdditionalData
                    {
                        MerchantReferenceData = order.UniqueId.ToString(),
                        //order.UserID,
                        //order.StoreID,
                        //order.WebCoupon,
                        //order.CreditUsed,
                    },
                });

                if (resp.IsSuccess)
                {
                    _logger.LogInformation($"CompleteVirtualCardPayment - order.ID: {order.UniqueId} - Payment Success");

                    order.Paid = true;
                    order.StatusCode = resp.ResponseCode;
                    order.Completed = DateTime.Now;
                    order.Card = resp.MaskedCardNumber;

                    var sendOrderResponse = await _orderService.Send3dSecureOrderAsync(
                        store,
                        order,
                        order,
                        _orderService.CreateValitorPayReceipt(order, store, resp));

                    _logger.LogDebug("sendOrderResponse: " + sendOrderResponse.Dump());

                    if (!sendOrderResponse.Success)
                    {
                        response.Content = CreateScriptContent(
                            false,
                            errorCode: sendOrderResponse.Message ?? Settings.InternalServerErrorCode);
                        response.StatusCode = HttpStatusCode.InternalServerError;
                        return ResponseMessage(response);
                    }
                }
                else
                {
                    HttpStatusCode httpStatusCode = HttpStatusCode.InternalServerError;

                    if (Vettvangur.ValitorPay.StatusCodes.CodeMap.TryGetValue(resp.ResponseCode, out var statusCode))
                    {
                        response.Content = CreateScriptContent(
                            false,
                            errorCode: StatusFormatter(statusCode.Type));

                        if (statusCode.Type != CodeType.ServerError)
                        {
                            httpStatusCode = HttpStatusCode.Unauthorized;
                        }
                    }
                    else
                    {
                        response.Content = CreateScriptContent(
                            false,
                            errorCode: Settings.InternalServerErrorCode);
                    }

                    response.StatusCode = httpStatusCode;
                    return ResponseMessage(response);
                }
            }
            else if (MDStatusCodes.CodeMap[notificationCallBack.MdStatus].Retryable
                && !query.AllKeys.Contains("retry"))
            {
                var req = new CardVerificationRequest
                {
                    DisplayName = _settings.ValitorPayDisplayName,

                    VirtualCard = md.VirtualCard,
                    Amount = order.Amount * 100,
                    AuthenticationUrl
                        = new Uri($"{Request.RequestUri.Scheme}://{Request.RequestUri.Authority}"
                        + "/api/ValitorPay/completeVirtualCardPayment?retry=1"),
                };
                req.SetMerchantData(md, secret);

                if (_settings.Environment == "dev")
                {
                    req.ThreeDs20AdditionalParamaters = new ThreeDs20AdditionalParamaters
                    {
                        ThreeDs2XGeneralExtrafields = new ThreeDs2XGeneralExtrafields
                        {
                            ThreeDsRequestorChallengeInd = ThreeDsRequestorChallenge.ChallengeRequested_Mandate
                        }
                    };
                }

                var resp = await _valitorPayService.CardVerificationAsync(req);
                if (resp.IsSuccess)
                {
                    response.Content = CreateScriptContent(
                            false,
                            errorCode: null,
                            valitorHtml: resp.CardVerificationRawResponse);

                    response.StatusCode = (HttpStatusCode)230;
                    return ResponseMessage(response);
                }
            }
            else
            {
                HttpStatusCode httpStatusCode = HttpStatusCode.InternalServerError;
                if (MDStatusCodes.CodeMap.TryGetValue(notificationCallBack.MdStatus, out var statusCode))
                {
                    response.Content = CreateScriptContent(
                        false,
                        errorCode: StatusFormatter(statusCode.Type));

                    if (statusCode.Type != CodeType.ServerError)
                    {
                        httpStatusCode = HttpStatusCode.Unauthorized;
                    }
                }
                else
                {
                    response.Content = CreateScriptContent(
                        false,
                        errorCode: Settings.InternalServerErrorCode);
                }

                response.StatusCode = httpStatusCode;
                return ResponseMessage(response);
            }
        }
        catch (Exception ex)
        {
            if (order != null)
            {
                order.Updated = DateTime.Now;

                if (ex is ValitorPayResponseException valitorEx)
                {
                    order.StatusCode = valitorEx.ValitorResponse?.ResponseCode;
                }

                _db.orders.Update(order);
                _db.Save();
            }

            // We log valitor response when ValitorPayResponseException inside valitorPay library
            _logger.LogError(ex);

            response.Content = CreateScriptContent(
                false,
                errorCode: Settings.InternalServerErrorCode);
            response.StatusCode = HttpStatusCode.InternalServerError;

            return ResponseMessage(response);
        }

        response.Content = CreateScriptContent(true);

        return ResponseMessage(response);
    }

    /// <summary>
    /// Initial payment with card, optionally stores virtual card afterwards
    /// </summary>
    /// <param name="notificationCallBack"></param>
    /// <returns></returns>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/valitorPay/completeFirstPayment")]
    [HttpPost]
    public async Task<IActionResult> CompleteFirstPayment(CardVerificationCallback notificationCallBack)
    {
        _logger.LogInformation("CompleteFirstPayment");

        var response = new HttpResponseMessage { };
        order order = null;

        try
        {
            var secret = _settings.ValitorPayApiKey
                .Split('.')
                .Last();

            var md = notificationCallBack.GetMerchantData<MerchantDataCard>(secret);

            order = await _db.orders.FirstOrDefaultAsync(m => m.ID.ToString() == md.OrderId);

            _logger.LogInformation("CompleteFirstPayment - order.ID: " + order.ID);

            if (order == null)
            {
                _logger.LogWarning("No order found for id: " + md.OrderId);
                return NotFound();
            }

            if (order.IsPayed)
            {
                _logger.LogWarning("Order already paid: " + md.OrderId);
                return Ok();
            }

            order.DsTransID = notificationCallBack.TDS2.DsTransID;
            order.Updated = DateTime.Now;

            var order = JsonConvert.DeserializeObject<OrderView>(order.Order);
            var store = await _db.Stores.FirstOrDefaultAsync(s => s.RefID == order.StoreID);

            // Remove encrypted card information before logging
            notificationCallBack.MD = null;
            _logger.LogDebug(JsonConvert.SerializeObject(notificationCallBack));

            if (!MDStatusCodes.CodeMap.ContainsKey(notificationCallBack.MdStatus))
            {
                throw new NotSupportedException("Unknown MdStatus code");
            }

            _valitorPayService.ConfigureAgreement(store.AgreementNumber, store.TerminalId);

            var query = Request.RequestUri.ParseQueryString();

            if (MDStatusCodes.CodeMap[notificationCallBack.MdStatus].Type == CodeType.Success
            && VerifySignature(md, notificationCallBack.Signature))
            {
                _logger.LogInformation($"CompleteFirstPayment - order.ID: {order.ID} - md status success");

                var resp = await _valitorPayService.CardPaymentAsync(new CardPaymentRequest
                {
                    Operation = Operation.Sale,

                    CardNumber = md.CardNumber,
                    ExpirationMonth = md.ExpirationMonth,
                    ExpirationYear = md.ExpirationYear,
                    CVC = md.Cvc,

                    CardVerificationData = new CardVerificationData
                    {
                        DsTransId = notificationCallBack.TDS2.DsTransID,
                        Cavv = notificationCallBack.Cavv,
                        Xid = notificationCallBack.Xid,
                        MdStatus = notificationCallBack.MdStatus,
                    },
                    Amount = order.Amount * 100,
                    AdditionalData = new CardPaymentAdditionalData
                    {
                        MerchantReferenceData = order.ID,
                        //order.UserID,
                        //order.StoreID,
                        //order.WebCoupon,
                        //order.CreditUsed,
                    },
                    FirstTransactionData = new FirstTransactionData
                    {
                        InitiationReason = InitiationReason.CredentialOnFile,
                    },
                });

                if (resp.IsSuccess)
                {
                    _logger.LogInformation($"CompleteFirstPayment - order.ID: {order.ID} - Payment Success");

                    order.IsPayed = true;
                    order.IsPayed = true;
                    order.StatusCode = resp.ResponseCode;
                    order.Completed = DateTime.Now;
                    order.DsTransID = notificationCallBack.TDS2.DsTransID;
                    order.Card = resp.MaskedCardNumber;

                    var sendOrderResponse = await _orderService.Send3dSecureOrderAsync(
                        store,
                        order,
                        order,
                        _orderService.CreateValitorPayReceipt(order, store, resp),
                        new PaymentMethod
                        {
                            CardNumber = md.CardNumber,
                            Month = md.ExpirationMonth,
                            Year = md.ExpirationYear,
                            CVC = md.Cvc,
                            SaveNewCard = order.PaymentMethod.SaveNewCard,
                        });

                    _logger.LogDebug("sendOrderResponse: " + sendOrderResponse.Dump());

                    if (!sendOrderResponse.Success)
                    {
                        response.Content = CreateScriptContent(
                            false,
                            errorCode: sendOrderResponse.Message ?? Settings.InternalServerErrorCode);
                        response.StatusCode = HttpStatusCode.InternalServerError;
                        return ResponseMessage(response);
                    }
                }
                else
                {
                    HttpStatusCode httpStatusCode = HttpStatusCode.InternalServerError;

                    if (Vettvangur.ValitorPay.StatusCodes.CodeMap.TryGetValue(resp.ResponseCode, out var statusCode))
                    {
                        response.Content = CreateScriptContent(
                            false,
                            errorCode: StatusFormatter(statusCode.Type));

                        if (statusCode.Type != CodeType.ServerError)
                        {
                            httpStatusCode = HttpStatusCode.Unauthorized;
                        }
                    }
                    else
                    {
                        response.Content = CreateScriptContent(
                            false,
                            errorCode: Settings.InternalServerErrorCode);
                    }

                    response.StatusCode = httpStatusCode;
                    return ResponseMessage(response);
                }
            }
            else if (MDStatusCodes.CodeMap[notificationCallBack.MdStatus].Retryable
                && !query.AllKeys.Contains("retry"))
            {
                var req = new CardVerificationRequest
                {
                    CardNumber = md.CardNumber,
                    ExpirationMonth = md.ExpirationMonth,
                    ExpirationYear = md.ExpirationYear,
                    Amount = order.Amount * 100,
                    AuthenticationUrl
                        = new Uri($"{Request.RequestUri.Scheme}://{Request.RequestUri.Authority}"
                        + "/api/ValitorPay/completeFirstPayment?retry=1"),
                };
                req.SetMerchantData(md, secret);

                if (_settings.Environment == "dev")
                {
                    req.ThreeDs20AdditionalParamaters = new ThreeDs20AdditionalParamaters
                    {
                        ThreeDs2XGeneralExtrafields = new ThreeDs2XGeneralExtrafields
                        {
                            ThreeDsRequestorChallengeInd = ThreeDsRequestorChallenge.ChallengeRequested_Mandate
                        }
                    };
                }

                var resp = await _valitorPayService.CardVerificationAsync(req);
                if (resp.IsSuccess)
                {
                    response.Content = CreateScriptContent(
                            false,
                            errorCode: null,
                            valitorHtml: resp.CardVerificationRawResponse);
                    response.StatusCode = (HttpStatusCode)230;

                    return ResponseMessage(response);
                }
            }
            else
            {
                HttpStatusCode httpStatusCode = HttpStatusCode.InternalServerError;
                if (MDStatusCodes.CodeMap.TryGetValue(notificationCallBack.MdStatus, out var statusCode))
                {
                    response.Content = CreateScriptContent(
                        false,
                        errorCode: StatusFormatter(statusCode.Type));

                    if (statusCode.Type != CodeType.ServerError)
                    {
                        httpStatusCode = HttpStatusCode.Unauthorized;
                    }
                }
                else
                {
                    response.Content = CreateScriptContent(
                        false,
                        errorCode: Settings.InternalServerErrorCode);
                }

                response.StatusCode = httpStatusCode;
                return ResponseMessage(response);
            }
        }
        catch (Exception ex)
        {
            if (order != null)
            {
                order.Updated = DateTime.Now;

                if (ex is ValitorPayResponseException valitorEx)
                {
                    order.StatusCode = valitorEx.ValitorResponse?.ResponseCode;
                }

                _db.orders.Update(order);
                _db.Save();
            }

            // We log valitor response when ValitorPayResponseException inside valitorPay library
            _logger.LogError(ex);

            response.Content = CreateScriptContent(
                false,
                errorCode: Settings.InternalServerErrorCode);
            response.StatusCode = HttpStatusCode.InternalServerError;

            return ResponseMessage(response);
        }

        response.Content = CreateScriptContent(true);

        return ResponseMessage(response);
    }

    /// <summary>
    /// Currently unused per Valitor instructions
    /// If we don't get functional xid and cavv values in callback, payment will never proceed anyway
    /// </summary>
    /// <returns></returns>
    private bool VerifySignature(object merchantData, string signature)
    {
        return true;

        //try
        //{
        //    using (SHA512 shaM = new SHA512Managed())
        //    {
        //        var data = Encoding.UTF8.GetBytes(
        //            _settings.ValitorPayApiKey +
        //            "|" +
        //            JsonConvert.SerializeObject(merchantData));
        //        var hash = shaM.ComputeHash(data);

        //        if (hash.ToString() != signature)
        //        {
        //            _logger.LogDebug("SHA match");
        //        }
        //        else
        //        {
        //            _logger.LogWarning("SHA mismatch hash: " + hash.ToString() + " signature: " + notificationCallBack.Signature);
        //        }
        //    }
        //}
        //catch (Exception ex)
        //{
        //    _logger.LogError(ex, "hash failed");
        //}
    }

    private string StatusFormatter(CodeType codeType)
    {
        switch (codeType)
        {
            case CodeType.SecurityEvent:
            case CodeType.UserError:
                return "ekki_tokst_ad_rukka_kort"; // kortanumer_ogilt

            case CodeType.InsufficientFunds:
                return "ekki_heimild";

            case CodeType.ServerError:
            default:
                return Settings.InternalServerErrorCode;
        }
    }

    private StringContent CreateScriptContent(
        bool success,
        string errorCode = null,
        string valitorHtml = null)
    {
        var content = new StringContent(
            "<script>" +
            //"document.dominosPayment = { " +
            //"success: " + success.ToString().ToLower() + ", " +
            //"errorCode: '" + errorCode + "', " +
            //"valitorHtml: '" + (valitorHtml == null
            //    ? valitorHtml
            //    : Convert.ToBase64String(Encoding.UTF8.GetBytes(valitorHtml))) + "' }; " +
            "try { window.parent.postMessage({ " +
            "success: " + success.ToString().ToLower() + ", " +
            "errorCode: '" + errorCode + "', " +
            "valitorHtml: '" + (valitorHtml == null
                ? valitorHtml
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(valitorHtml))) + "' }, " +
            "'*'); } catch(err) {}</script>");

        content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        return content;
    }
}

