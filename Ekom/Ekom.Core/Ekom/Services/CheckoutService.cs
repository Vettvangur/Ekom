using Ekom.API;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Utilities;
using Microsoft.Extensions.Logging;

namespace Ekom.Services;

/// <summary>
/// Handles order finalisation after successful payment or order completion
/// </summary>
class CheckoutService
{
    readonly ILogger<CheckoutService> _logger;
    readonly OrderRepository _orderRepo;
    readonly CouponRepository _couponRepo;
    readonly OrderService _orderService;
    readonly IOrderActivityLogService _orderActivityLogService;
    readonly IMailService _mailService;
    readonly CheckoutReservationService _checkoutReservations;

    public CheckoutService(
        ILogger<CheckoutService> logger,
        Configuration config,
        OrderRepository orderRepo,
        CouponRepository couponRepo,
        OrderService orderService,
        DiscountStockRepository discountStockRepo,
        IOrderActivityLogService orderActivityLogService,
        IMailService mailService,
        IStockReservationService reservations,
        CheckoutReservationService checkoutReservations)
    {
        _logger = logger;
        _orderRepo = orderRepo;
        _couponRepo = couponRepo;
        _orderService = orderService;
        _orderActivityLogService = orderActivityLogService;
        _mailService = mailService;
        _checkoutReservations = checkoutReservations;
    }

    public async Task CompleteAsync(Guid key, CancellationToken ct = default)
    {
        OrderData? o = null;
        OrderInfo? oi = null;

        try
        {
            _logger.LogInformation("Completing order {OrderId}", key);

            o = await _orderRepo.GetOrderAsync(key, ct).ConfigureAwait(false);

            if (o == null)
            {
                return;
            }

            oi = new OrderInfo(o);

            CompleteCheckoutEventArgs model = new CompleteCheckoutEventArgs()
            {
                OrderInfo = oi,
                StockValidation = true,
                UpdateOrderStatus = o.OrderStatus == OrderStatus.OfflinePayment ? false : true,
                OrderData = o
            };

            CheckoutEvents.OnCompleteCheckout(this, model);
            await CheckoutEvents.OnCompleteCheckoutAsync(this, model);

            if (!await _checkoutReservations.IsCompletedAsync(key, ct).ConfigureAwait(false))
            {
                var requirements = await _checkoutReservations.GetRequirementsAsync(oi, ct, model.StockValidation).ConfigureAwait(false);
                await _checkoutReservations.CompleteStockAsync(key, requirements, oi.ReservationIds, model.StockValidation, ct)
                    .ConfigureAwait(false);
            }

            if (oi.Discount != null)
            {
                try
                {
                    //discount eventar virka ekki (vilt líklega hlusta frekar eftir coupon, þurfum þá coupon klasa og henda honum á orderinfo og orderline og breyta "öllu")
                    //oi.Discount?.OnCouponApply();

                    if (!string.IsNullOrEmpty(oi.Coupon))
                    {
                        await _couponRepo.MarkUsedAsync(oi.Coupon)
                            .ConfigureAwait(false);
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex) // Swallow all event subscriber exceptions
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    _logger.LogError(ex, "Error on marking coupon used");
                }
            }

            if (model.UpdateOrderStatus)
            {
                await _orderService.ChangeOrderStatusAsync(o.UniqueId, OrderStatus.ReadyForDispatch)
                    .ConfigureAwait(false);
            }

            string completionMessage = model.UpdateOrderStatus
                ? "Order Completed."
                : "Order Completed. Offline payment.";

            await _orderActivityLogService.AddOrderLogAsync(
                    o.UniqueId,
                    completionMessage,
                    logType: OrderActivityLogType.Success,
                    ct: ct)
                .ConfigureAwait(false);
        }
        catch (NotEnoughStockException ex)
        {
            _logger.LogError(
                ex,
                $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}. " +
                $"Order id: {oi?.UniqueId}");

            string subject
                = $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}. "
                + $"Order id: {oi?.UniqueId}";
            string body
                = $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}."
                + $"Order id: {oi?.UniqueId}\r\n";

            if (ex is NotEnoughLineStockException exl)
            {
                body += $"Line {exl.OrderLineKey} and variant == {exl.Variant == true}";
            }

            body += ex.ToString();

            await _mailService.SendAsync(subject, body).ConfigureAwait(false);

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected checkout error");
            string subject
                = $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}. "
                + $"Order id: {oi?.UniqueId}";
            string body
                = $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}."
                + $"Order id: {oi?.UniqueId}\r\n\r\n" + ex.ToString();

            await _mailService.SendAsync(subject, body).ConfigureAwait(false);

            throw;
        }
    }

}
