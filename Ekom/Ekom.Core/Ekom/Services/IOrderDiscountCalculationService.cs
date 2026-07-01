using Ekom.Models;

namespace Ekom.Services;

public interface IOrderDiscountCalculationService
{
    Task<OrderDiscountCalculationResult> CalculateByCouponAsync(
        OrderDiscountCalculationRequest request,
        CancellationToken ct = default);
}
