using Ekom.Models;
using Ekom.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Ekom.Controllers;

[Route("ekom/order-discounts")]
[ServiceFilter(typeof(ApiExceptionFilter))]
public class EkomOrderDiscountsController : ControllerBase
{
    private const string ApiKeyHeaderName = "X-Ekom-Api-Key";

    private readonly IOrderDiscountCalculationService _orderDiscountCalculationService;
    private readonly IOptions<OrderDiscountCalculationOptions> _options;

    public EkomOrderDiscountsController(
        IOrderDiscountCalculationService orderDiscountCalculationService,
        IOptions<OrderDiscountCalculationOptions> options)
    {
        _orderDiscountCalculationService = orderDiscountCalculationService;
        _options = options;
    }

    [HttpPost]
    [Route("calculate")]
    [EnableRateLimiting("order-coupon")]
    public async Task<IActionResult> CalculateAsync(
        [FromBody] OrderDiscountCalculationRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
        {
            return BadRequest();
        }

        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        var result = await _orderDiscountCalculationService.CalculateByCouponAsync(request, ct)
            .ConfigureAwait(false);

        return Ok(result);
    }
    
    [HttpPost]
    [Route("update-stock")]
    public async Task<IActionResult> UpdateStock(
        [FromBody] OrderDiscountStockUpdateRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
        {
            return BadRequest();
        }

        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        if (request.Value == 0)
        {
            return BadRequest("Value can not be 0");
        }

        await API.Stock.Instance.UpdateDiscountStockAsync(request.Key, request.Value, request.Coupon)
            .ConfigureAwait(false);

        return Ok();
    }

    private bool IsAuthorized()
    {
        var configuredApiKey = _options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            return false;
        }

        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyValues))
        {
            return false;
        }

        var providedApiKey = apiKeyValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return false;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configuredApiKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedApiKey);

        return configuredBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
    }
}
