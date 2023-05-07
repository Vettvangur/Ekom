using Ekom.Payments;
using Ekom.Payments.Helpers;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Ekom.Payments.AsynchronousExample;

/// <summary>
/// Receives a callback from AsynchronousExample when customer completes payment.
/// Changes order status and optionally runs a custom callback provided by the application consuming this library.
/// </summary>
[Route("ekom/payments/[controller]")]
[ApiController]
public class AsynchronousExampleResponseController : ControllerBase
{
    readonly ILogger _logger;
    readonly PaymentsConfiguration _settings;
    readonly IOrderService _orderService;
    readonly IDatabaseFactory _dbFac;
    readonly IMailService _mailSvc;

    /// <summary>
    /// ctor
    /// </summary>
    public AsynchronousExampleResponseController(
        ILogger<AsynchronousExampleResponseController> logger,
        PaymentsConfiguration settings,
        IOrderService orderService,
        IDatabaseFactory dbFac,
        IMailService mailSvc)
    {
        _logger = logger;
        _settings = settings;
        _orderService = orderService;
        _dbFac = dbFac;
        _mailSvc = mailSvc;
    }

    /// <summary>
    /// Receives a callback from AsynchronousExample when customer completes payment.
    /// Changes order status and optionally runs a custom callback provided by the application consuming this library.
    /// </summary>
    /// <param name="AsynchronousExampleResp">AsynchronousExample querystring parameters</param>
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet, HttpPost]
    [Route("")]
    public async Task<IActionResult> Post([FromQuery] Response AsynchronousExampleResp)
    {
        _logger.LogInformation("AsynchronousExample Payment Response - Start");

        _logger.LogDebug(JsonConvert.SerializeObject(AsynchronousExampleResp));

        if (AsynchronousExampleResp != null && ModelState.IsValid)
        {
            try
            {
                _logger.LogDebug("AsynchronousExample Payment Response - ModelState.IsValid");

                if (!Guid.TryParse(AsynchronousExampleResp.ReferenceNumber, out var orderId))
                {
                    return BadRequest();
                }

                _logger.LogInformation("AsynchronousExample Payment Response - OrderID: " + orderId);

                OrderStatus? order = await _orderService.GetAsync(orderId);
                if (order == null)
                {
                    _logger.LogWarning("AsynchronousExample Payment Response - Unable to find order {OrderId}", orderId);

                    return NotFound();
                }
                var paymentSettings = JsonConvert.DeserializeObject<PaymentSettings>(order.EkomPaymentSettingsData);
                var AsynchronousExampleSettings = JsonConvert.DeserializeObject<AsynchronousExampleSettings>(order.EkomPaymentProviderData);

                // perform payment provider specific security validation here
                bool isValid = true;

                if (isValid)
                {
                    _logger.LogInformation("AsynchronousExample Payment Response - isValid");

                    if (!order.Paid)
                    {
                        try
                        {
                            var paymentData = new PaymentData
                            {
                                Id = order.UniqueId,
                                Date = DateTime.Now,
                                CardNumber = AsynchronousExampleResp.CardNumberMasked,
                                CustomData = JsonConvert.SerializeObject(AsynchronousExampleResp),
                                Amount = order.Amount.ToString(),
                            };

                            using var db = _dbFac.GetDatabase();
                            await db.InsertOrReplaceAsync(paymentData);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "AsynchronousExample Payment Response - Error saving payment data");
                        }

                        order.Paid = true;

                        using (var db = _dbFac.GetDatabase())
                        {
                            await db.UpdateAsync(order);
                        }

                        Events.OnSuccess(this, new SuccessEventArgs
                        {
                            OrderStatus = order,
                        });
                        _logger.LogInformation($"AsynchronousExample Payment Response - SUCCESS - Order ID: {order.UniqueId}");
                    }
                    else
                    {
                        _logger.LogInformation($"AsynchronousExample Payment Response - SUCCESS - Previously validated");
                    }

                    var successUrl = PaymentsUriHelper.EnsureFullUri(paymentSettings.SuccessUrl.ToString(), Request);
                    return Redirect(successUrl.ToString());
                }
                else
                {
                    _logger.LogInformation($"AsynchronousExample Payment Response - Verification Error - Order ID: {order.UniqueId}");
                    Events.OnError(this, new ErrorEventArgs
                    {
                        OrderStatus = order,
                    });

                    var cancelUrl = PaymentsUriHelper.EnsureFullUri(paymentSettings.CancelUrl.ToString(), Request);
                    return Redirect(cancelUrl.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AsynchronousExample Payment Response - Failed");
                Events.OnError(this, new ErrorEventArgs
                {
                    Exception = ex,
                });

                if (_settings.SendEmailAlerts)
                {
                    await _mailSvc.SendAsync(new System.Net.Mail.MailMessage
                    {
                        Subject = "AsynchronousExample Payment Response - Failed",
                        Body = $"<p>AsynchronousExample Payment Response - Failed<p><br />{HttpContext.Request.GetDisplayUrl()}<br />" + ex.ToString(),
                        IsBodyHtml = true,
                    });
                }

                throw;
            }
        }

        _logger.LogDebug(JsonConvert.SerializeObject(ModelState));
        return Redirect("/");
    }
}
