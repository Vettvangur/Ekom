using Ekom.Models;
using Ekom.Repositories;
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

    private readonly CouponRepository _couponRepository;
    private readonly IOrderDiscountCalculationService _orderDiscountCalculationService;
    private readonly IOptions<OrderDiscountCalculationOptions> _options;

    public EkomOrderDiscountsController(
        CouponRepository couponRepository,
        IOrderDiscountCalculationService orderDiscountCalculationService,
        IOptions<OrderDiscountCalculationOptions> options)
    {
        _couponRepository = couponRepository;
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
    [Route("coupon/mark-used")]
    [EnableRateLimiting("order-coupon")]
    public async Task<IActionResult> MarkCouponUsedAsync([FromBody] CouponRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.coupon))
        {
            return BadRequest("Coupon code can not be empty");
        }

        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        await _couponRepository.MarkUsedAsync(request.coupon)
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
