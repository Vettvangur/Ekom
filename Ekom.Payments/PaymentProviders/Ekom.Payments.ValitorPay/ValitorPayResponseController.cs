using Ekom.Payments;
using Ekom.Payments.Helpers;
using Ekom.Payments.ValitorPay;
using LinqToDB;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
    readonly IWebHostEnvironment _webHostEnvironment;

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
        ValitorPayService valitorPayService,
        IWebHostEnvironment webHostEnvironment)
    {
        _logger = logger;
        _config = config;
        _settings = settings;
        _orderService = orderService;
        _dbFac = dbFac;
        _mailSvc = mailSvc;
        _valitorPayService = valitorPayService;
        _webHostEnvironment = webHostEnvironment;
    }

    /// <summary>
    /// <param name="notificationCallBack"></param>
    /// </summary>
    /// <returns></returns>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("completeVirtualCardPayment")]
    [HttpPost]
    public async Task<IActionResult> CompleteVirtualCardPayment([FromForm] CardVerificationCallback notificationCallBack)
    {
        _logger.LogInformation("CompleteVirtualCardPayment");

        OrderStatus? order = null;

        if (!MDStatusCodes.CodeMap.ContainsKey(notificationCallBack.MdStatus))
        {
            return BadRequest();
        }

        var valitorPayData = new Dictionary<string, string>
            {
                { "dsTransId", notificationCallBack.TDS2.DsTransID },
                { "updated", DateTime.Now.ToString() }
            };

        try
        {
            var apiKey = _config["Ekom:Payments:ValitorPay:ApiKey"];
            var secret = apiKey
                .Split('.')
                .Last();
            var md = notificationCallBack.GetMerchantData<MerchantDataVirtualCard>(secret);

            _logger.LogInformation("CompleteVirtualCardPayment - OrderID: " + md.OrderId);

            order = await _orderService.GetAsync(Guid.Parse(md.OrderId));

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
            
            // Remove encrypted card information before logging
            notificationCallBack.MD = null;
            _logger.LogDebug(JsonConvert.SerializeObject(notificationCallBack));

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
                        MerchantReferenceData = order.UniqueId.ToString().Replace("-", ""),
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
                    valitorPayData.Add("statusCode", resp.ResponseCode);
                    valitorPayData.Add("completed", DateTime.Now.ToString());
                    valitorPayData.Add("card", resp.MaskedCardNumber);

                    order!.EkomPaymentProviderData = JsonConvert.SerializeObject(valitorPayData);
                    await _orderService.UpdateAsync(order);
                }
                else
                {
                    ContentResult? content = null;

                    if (Vettvangur.ValitorPay.StatusCodes.CodeMap.TryGetValue(resp.ResponseCode, out var statusCode))
                    {
                        content = CreateScriptContent(
                            false,
                            errorCode: statusCode.Type.ToString());

                        if (statusCode.Type != CodeType.ServerError)
                        {
                            content.StatusCode = (int)HttpStatusCode.Unauthorized;
                        }
                    }
                    else
                    {
                        content = CreateScriptContent(
                            false,
                            errorCode: "ServerError");
                        content.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }

                    order!.EkomPaymentProviderData = JsonConvert.SerializeObject(valitorPayData);
                    await _orderService.UpdateAsync(order);

                    Events.OnError(this, new ErrorEventArgs
                    {
                        OrderStatus = order,
                    });

                    return content;
                }
            }
            else if (MDStatusCodes.CodeMap[notificationCallBack.MdStatus].Retryable
                && !query.Keys.Contains("retry"))
            {
                var req = new CardVerificationRequest
                {
                    DisplayName = valitorSettings.PaymentPortalDisplayName,

                    VirtualCard = md.VirtualCard,
                    Amount = (long)(order.Amount * 100),
                    AuthenticationUrl
                        = new Uri($"{Request.Scheme}://{Request.Host}"
                        + "/api/ValitorPay/completeVirtualCardPayment?retry=1"),
                };
                req.SetMerchantData(md, secret);

                if (!_webHostEnvironment.IsProduction())
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
                    var content = CreateScriptContent(
                            false,
                            errorCode: null,
                            valitorHtml: resp.CardVerificationRawResponse);
                    content.StatusCode = 230;

                    order!.EkomPaymentProviderData = JsonConvert.SerializeObject(valitorPayData);
                    await _orderService.UpdateAsync(order);

                    return content;
                }
            }
            else
            {
                var statusCode = MDStatusCodes.CodeMap[notificationCallBack.MdStatus]!;

                var content = CreateScriptContent(
                    false,
                    errorCode: statusCode.Type.ToString());

                if (statusCode.Type != CodeType.ServerError)
                {
                    content.StatusCode = (int)HttpStatusCode.Unauthorized;
                }

                order!.EkomPaymentProviderData = JsonConvert.SerializeObject(valitorPayData);
                await _orderService.UpdateAsync(order);

                Events.OnError(this, new ErrorEventArgs
                {
                    OrderStatus = order,
                });

                return content;
            }
        }
#pragma warning disable CA1031 // We log and return 500, so no swallowing
        catch (Exception ex)
        {
            if (order != null)
            {
                valitorPayData["updated"] = DateTime.Now.ToString();

                if (ex is ValitorPayResponseException valitorEx)
                {
                    valitorPayData.Add("statusCode", valitorEx.ValitorResponse?.ResponseCode!);
                }

                order!.EkomPaymentProviderData = JsonConvert.SerializeObject(valitorPayData);
                await _orderService.UpdateAsync(order);
            }

            // We log valitor response when ValitorPayResponseException inside valitorPay library
            _logger.LogError(ex, "CompleteVirtualCardPayment Exception");

            var content = CreateScriptContent(
                false,
                errorCode: notificationCallBack.MdStatus.ToString());

            content.StatusCode = (int)HttpStatusCode.InternalServerError;

            Events.OnError(this, new ErrorEventArgs
            {
                OrderStatus = order,
                Exception = ex,
            });

            return content;
        }
#pragma warning restore CA1031 // Do not catch general exception types

        Events.OnSuccess(this, new SuccessEventArgs
        {
            OrderStatus = order,
        });

        return CreateScriptContent(true);
    }

    /// <summary>
    /// Initial payment with card, optionally stores virtual card afterwards
    /// </summary>
    /// <param name="notificationCallBack"></param>
    /// <returns></returns>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("completeFirstPayment")]
    [HttpPost]
    public async Task<IActionResult> CompleteFirstPayment([FromForm] CardVerificationCallback notificationCallBack)
    {
        _logger.LogInformation("CompleteFirstPayment");

        OrderStatus? order = null;

        if (!MDStatusCodes.CodeMap.ContainsKey(notificationCallBack.MdStatus))
        {
            return BadRequest();
        }

        var valitorPayData = new Dictionary<string, string>
            {
                { "dsTransId", notificationCallBack.TDS2.DsTransID },
                { "updated", DateTime.Now.ToString() }
            };

        try
        {
            var apiKey = _config["Ekom:Payments:ValitorPay:ApiKey"];
            var secret = apiKey
                .Split('.')
                .Last();

            var md = notificationCallBack.GetMerchantData<MerchantDataCard>(secret);

            _logger.LogInformation("CompleteFirstPayment - OrderID: " + md.OrderId);

            order = await _orderService.GetAsync(Guid.Parse(md.OrderId));

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

            // Remove encrypted card information before logging
            notificationCallBack.MD = null;
            _logger.LogDebug(JsonConvert.SerializeObject(notificationCallBack));

            var paymentSettings = JsonConvert.DeserializeObject<PaymentSettings>(order.EkomPaymentSettingsData);
            var valitorSettings = JsonConvert.DeserializeObject<ValitorPaySettings>(order.EkomPaymentProviderData);

            _valitorPayService.ConfigureAgreement(valitorSettings.AgreementNumber, valitorSettings.TerminalId);

            var query = Request.Query;

            if (MDStatusCodes.CodeMap[notificationCallBack.MdStatus].Type == CodeType.Success
            && VerifySignature(md, notificationCallBack.Signature))
            {
                _logger.LogInformation($"CompleteFirstPayment - order.ID: {order.UniqueId} - MD Status Success");

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
                    Amount = (long)(order.Amount * 100),
                    AdditionalData = new CardPaymentAdditionalData
                    {
                        MerchantReferenceData = order.UniqueId.ToString().Replace("-", ""),
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
                    _logger.LogInformation($"CompleteFirstPayment - order.ID: {order.UniqueId} - Payment Success");

                    order.Paid = true;
                    valitorPayData.Add("statusCode", resp.ResponseCode);
                    valitorPayData.Add("completed", DateTime.Now.ToString());
                    valitorPayData.Add("card", resp.MaskedCardNumber);

                    order!.EkomPaymentProviderData = JsonConvert.SerializeObject(valitorPayData);
                    await _orderService.UpdateAsync(order);
                }
                else
                {
                    ContentResult? content = null;

                    if (Vettvangur.ValitorPay.StatusCodes.CodeMap.TryGetValue(resp.ResponseCode, out var statusCode))
                    {
                        content = CreateScriptContent(
                            false,
                            errorCode: statusCode.Type.ToString());

                        if (statusCode.Type != CodeType.ServerError)
                        {
                            content.StatusCode = (int)HttpStatusCode.Unauthorized;
                        }
                    }
                    else
                    {
                        content = CreateScriptContent(
                            false,
                            errorCode: "ServerError");
                        content.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }

                    order!.EkomPaymentProviderData = JsonConvert.SerializeObject(valitorPayData);
                    await _orderService.UpdateAsync(order);

                    return content;
                }
            }
            else if (MDStatusCodes.CodeMap[notificationCallBack.MdStatus].Retryable
                && !query.Keys.Contains("retry"))
            {
                var req = new CardVerificationRequest
                {
                    DisplayName = valitorSettings.PaymentPortalDisplayName,

                    CardNumber = md.CardNumber,
                    ExpirationMonth = md.ExpirationMonth,
                    ExpirationYear = md.ExpirationYear,
                    Amount = (long)(order.Amount * 100),
                    AuthenticationUrl
                        = new Uri($"{Request.Scheme}://{Request.Host}"
                        + "/ekom/payments/ValitorPay/completeFirstPayment?retry=1"),
                };
                req.SetMerchantData(md, secret);

                if (!_webHostEnvironment.IsProduction())
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
                    var content = CreateScriptContent(
                            false,
                            errorCode: null,
                            valitorHtml: resp.CardVerificationRawResponse);
                    content.StatusCode = 230;

                    order!.EkomPaymentProviderData = JsonConvert.SerializeObject(valitorPayData);
                    await _orderService.UpdateAsync(order);

                    return content;
                }
            }
            else
            {
                var statusCode = MDStatusCodes.CodeMap[notificationCallBack.MdStatus]!;

                var content = CreateScriptContent(
                    false,
                    errorCode: statusCode.Type.ToString());

                if (statusCode.Type != CodeType.ServerError)
                {
                    content.StatusCode = (int)HttpStatusCode.Unauthorized;
                }

                order!.EkomPaymentProviderData = JsonConvert.SerializeObject(valitorPayData);
                await _orderService.UpdateAsync(order);

                Events.OnError(this, new ErrorEventArgs
                {
                    OrderStatus = order,
                });

                return content;
            }
        }
#pragma warning disable CA1031 // We log and return 500, so no swallowing
        catch (Exception ex)
        {
            if (order != null)
            {
                valitorPayData["updated"] = DateTime.Now.ToString();

                if (ex is ValitorPayResponseException valitorEx)
                {
                    // lies, handles null value just fine
                    valitorPayData.Add("statusCode", valitorEx.ValitorResponse?.ResponseCode!);
                }

                order!.EkomPaymentProviderData = JsonConvert.SerializeObject(valitorPayData);
                await _orderService.UpdateAsync(order);
            }

            // We log valitor response when ValitorPayResponseException inside valitorPay library
            _logger.LogError(ex, "CompleteFirstPayment Exception");

            var content = CreateScriptContent(
                false,
                errorCode: notificationCallBack.MdStatus.ToString());

            content.StatusCode = (int) HttpStatusCode.InternalServerError;

            Events.OnError(this, new ErrorEventArgs
            {
                OrderStatus = order,
                Exception = ex,
            });

            return content;
        }
#pragma warning restore CA1031 // Do not catch general exception types

        Events.OnInitialPaymentSuccess(this, new SuccessEventArgs
        {
            OrderStatus = order,
        });

        return CreateScriptContent(true);
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

    private ContentResult CreateScriptContent(
        bool success,
        string? errorCode = null,
        string? valitorHtml = null)
    {
        var content = new ContentResult();
        content.Content = 
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
            "'*'); } catch(err) {}</script>";

        content.ContentType = "text/html";

        return content;
    }
}

