using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using Umbraco.Core.Logging;
using Umbraco.NetPayment.Exceptions;
using Umbraco.NetPayment.Helpers;
using Umbraco.NetPayment.Interfaces;
using Umbraco.Web.Mvc;

namespace Ekom.Payments.ValitorPay;

/// <summary>
/// Receives a callback from Valitor when customer completes payment.
/// Changes order status and optionally runs a custom callback provided by the application consuming this library.
/// </summary>
[PluginController("NetPayment")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Reliability",
    "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "Not needed in async controller actions")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Style",
    "VSTHRD200:Use \"Async\" suffix for async methods",
    Justification = "Async controller action")]
public class ValitorResponseController : SurfaceController
{
    readonly ILogger _logger;
    readonly PaymentsConfiguration _settings;
    readonly IOrderService _orderService;
    readonly IDatabaseFactory _dbFac;
    readonly IXMLConfigurationService _xmlSvc;
    readonly IMailService _mailSvc;
    readonly HttpRequestBase _req;

    /// <summary>
    /// ctor
    /// </summary>
    public ValitorResponseController(
        ILogger logger,
        PaymentsConfiguration settings,
        IOrderService orderService,
        IDatabaseFactory dbFac,
        IXMLConfigurationService xmlSvc,
        IMailService mailSvc,
        HttpContextBase httpContext)
    {
        _logger = logger;
        _settings = settings;
        _orderService = orderService;
        _dbFac = dbFac;
        _xmlSvc = xmlSvc;
        _mailSvc = mailSvc;
        _req = httpContext.Request;
    }

    /// <summary>
    /// Receives a callback from Valitor when customer completes payment.
    /// Changes order status and optionally runs a custom callback provided by the application consuming this library.
    /// </summary>
    /// <param name="valitorResp">Valitor querystring parameters</param>
    public async Task<HttpStatusCodeResult> Post([FromUri]Response valitorResp)
    {
        _logger.Info<ValitorResponseController>("Valitor Payment Response - Start");

        _logger.Debug<ValitorResponseController>(JsonConvert.SerializeObject(valitorResp));

        if (valitorResp != null && ModelState.IsValid)
        {
            try
            {
                _logger.Debug<ValitorResponseController>("ModelState.IsValid");

                if (!Guid.TryParse(valitorResp.ReferenceNumber, out var orderId))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                _logger.Info<ValitorResponseController>("Valitor Payment Response - OrderID: " + orderId);

                OrderStatus order = await _orderService.GetAsync(orderId);

                var xmlConfig = _xmlSvc.GetConfigForPP(order.PaymentProvider, Payment._ppNodeName);

                if (xmlConfig == null) throw new XmlConfigurationNotFoundException(order.PaymentProvider);

                string digitalSignature = CryptoHelpers.GetSHA256HexStringSum(xmlConfig["verificationcode"] + valitorResp.ReferenceNumber);

                if (valitorResp.DigitalSignatureResponse.Equals(digitalSignature, StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.Info<ValitorResponseController>("Valitor Payment Response - DigitalSignatureResponse Verified");

                    try
                    {
                        var paymentData = new PaymentData
                        {
                            Id = order.UniqueId,
                            Date = DateTime.Now,
                            PaymentDate = valitorResp.Date,
                            AuthorizationNumber = valitorResp.AuthorizationNumber.ToString(),
                            CardNumber = valitorResp.CardNumberMasked,
                            CardType = valitorResp.CardType,
                            Amount = order.Amount.ToString(),
                        };

                        PaymentData prevPaymentData;
                        using (var db = _dbFac.GetDb())
                        {
                            prevPaymentData = await db.SingleOrDefaultByIdAsync<PaymentData>(paymentData.Id);
                        }

                        using (var db = _dbFac.GetDb())
                        {
                            if (prevPaymentData == null)
                            {
                                await db.InsertAsync(paymentData);
                            }
                            else
                            {
                                await db.UpdateAsync(paymentData);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error<ValitorResponseController>(ex, "Valitor Payment Response - Error saving payment data");
                    }

                    order.Paid = true;

                    using (var db = _dbFac.GetDb())
                    {
                        await db.UpdateAsync(order);
                    }

                    LocalCallback.OnSuccess(order);
                    _logger.Info<ValitorResponseController>($"Valitor Payment Response - SUCCESS - Order ID: {order.UniqueId}");
                    return new HttpStatusCodeResult(HttpStatusCode.OK);
                }
                else
                {
                    _logger.Info<ValitorResponseController>($"Valitor Payment Response - Verification Error - Order ID: {order.UniqueId}");
                    LocalCallback.OnError(order, null);
                }
            }
            catch (Exception ex)
            {
                _logger.Error<ValitorResponseController>(ex, "Valitor Payment Response - Failed");
                LocalCallback.OnError(null, ex);

                if (_settings.SendEmailAlerts)
                {
                    _mailSvc.Subject = "Valitor Payment Response - Failed";
                    _mailSvc.Body = $"<p>Valitor Payment Response - Failed<p><br />{_req?.Url}<br />" + ex.ToString();
                    await _mailSvc.SendAsync();
                }

                throw;
            }
        }

        _logger.Debug<ValitorResponseController>(JsonConvert.SerializeObject(ModelState));
        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
    }
}
