using Ekom.Payments;
using Ekom.Payments.Helpers;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Umbraco.NetPayment.Valitor;

/// <summary>
/// Receives a callback from Valitor when customer completes payment.
/// Changes order status and optionally runs a custom callback provided by the application consuming this library.
/// </summary>
[Route("ekom/payments/[controller]")]
[ApiController]
public class ValitorResponseController : ControllerBase
{
    readonly ILogger _logger;
    readonly PaymentsConfiguration _settings;
    readonly IOrderService _orderService;
    readonly IDatabaseFactory _dbFac;
    readonly IMailService _mailSvc;
    readonly HttpRequest _req;

    /// <summary>
    /// ctor
    /// </summary>
    public ValitorResponseController(
        ILogger<ValitorResponseController> logger,
        PaymentsConfiguration settings,
        IOrderService orderService,
        IDatabaseFactory dbFac,
        IMailService mailSvc,
        HttpContext httpContext)
    {
        _logger = logger;
        _settings = settings;
        _orderService = orderService;
        _dbFac = dbFac;
        _mailSvc = mailSvc;
        _req = httpContext.Request;
    }

    /// <summary>
    /// Receives a callback from Valitor when customer completes payment.
    /// Changes order status and optionally runs a custom callback provided by the application consuming this library.
    /// </summary>
    /// <param name="valitorResp">Valitor querystring parameters</param>
    public async Task<IActionResult> Post([FromQuery]Response valitorResp)
    {
        _logger.LogInformation("Valitor Payment Response - Start");

        _logger.LogDebug(JsonConvert.SerializeObject(valitorResp));

        if (valitorResp != null && ModelState.IsValid)
        {
            try
            {
                _logger.LogDebug("ModelState.IsValid");

                if (!Guid.TryParse(valitorResp.ReferenceNumber, out var orderId))
                {
                    return BadRequest();
                }

                _logger.LogInformation("Valitor Payment Response - OrderID: " + orderId);

                OrderStatus order = await _orderService.GetAsync(orderId);

                string digitalSignature = CryptoHelpers.GetSHA256HexStringSum(xmlConfig["verificationcode"] + valitorResp.ReferenceNumber);

                if (valitorResp.DigitalSignatureResponse.Equals(digitalSignature, StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.LogInformation("Valitor Payment Response - DigitalSignatureResponse Verified");

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
                        using (var db = _dbFac.GetDatabase())
                        {
                            prevPaymentData = await db.PaymentData.SingleOrDefaultAsync(x => x.Id == paymentData.Id);
                        }

                        using (var db = _dbFac.GetDatabase())
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
                        _logger.LogError(ex, "Valitor Payment Response - Error saving payment data");
                    }

                    order.Paid = true;

                    using (var db = _dbFac.GetDb())
                    {
                        await db.UpdateAsync(order);
                    }

                    Events.OnSuccess(order);
                    _logger.LogInformation($"Valitor Payment Response - SUCCESS - Order ID: {order.UniqueId}");
                    return new HttpStatusCodeResult(HttpStatusCode.OK);
                }
                else
                {
                    _logger.LogInformation($"Valitor Payment Response - Verification Error - Order ID: {order.UniqueId}");
                    Events.OnError(order, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Valitor Payment Response - Failed");
                Events.OnError(null, ex);

                if (_settings.SendEmailAlerts)
                {
                    _mailSvc.Subject = "Valitor Payment Response - Failed";
                    _mailSvc.Body = $"<p>Valitor Payment Response - Failed<p><br />{_req?.Url}<br />" + ex.ToString();
                    await _mailSvc.SendAsync();
                }

                throw;
            }
        }

        _logger.LogDebug(JsonConvert.SerializeObject(ModelState));
        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
    }
}
