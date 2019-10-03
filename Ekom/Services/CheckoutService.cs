using Ekom.API;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Data;
using Ekom.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Core.Logging;

namespace Ekom.Services
{
    class CheckoutService
    {
        readonly ILogger _logger;
        readonly Configuration _config;
        readonly IDiscountStockRepository _discountStockRepo;
        readonly IOrderRepository _orderRepo;
        readonly ICouponRepository _couponRepo;
        private readonly OrderService _orderService;
        public CheckoutService(
            ILogger logger,
            Configuration config,
            IOrderRepository orderRepo,
            ICouponRepository couponRepo,
            OrderService orderService,
            IDiscountStockRepository discountStockRepo)
        {
            _logger = logger;
            _config = config;
            _orderRepo = orderRepo;
            _couponRepo = couponRepo;
            _orderService = orderService;
            _discountStockRepo = discountStockRepo;
        }

        public async Task CompleteAsync(Guid key)
        {
            OrderData o = null;
            OrderInfo oi = null;

            try
            {
                o = await _orderRepo.GetOrderAsync(key).ConfigureAwait(false);

                if (o == null) return;

                oi = new OrderInfo(o);

                foreach (var job in oi.HangfireJobs)
                {
                    Stock.Instance.CancelRollback(job);
                }

                if (oi.Discount != null)
                {
                    try
                    {
                        //discount eventar virka ekki baun (vilt líklega hlusta frekar eftir coupon, þurfum þá coupon klasa og henda honum á orderinfo og orderline og breyta "öllu")
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
                        _logger.Error<CheckoutService>(ex);
                    }
                }

                foreach (var line in oi.OrderLines.Where(line => line.Discount != null))
                {
                    if (!string.IsNullOrEmpty(line.Coupon))
                    {
                        var id = $"{line.Discount.Key}_{line.Coupon}";
                        await _discountStockRepo.UpdateAsync(id, -1)
                            .ConfigureAwait(false);
                    }

                    if (line.Discount.HasMasterStock)
                    {
                        await _discountStockRepo.UpdateAsync(line.Discount.Key.ToString(), -1)
                            .ConfigureAwait(false);
                    }

                    try
                    {
                        //discount eventar virka ekki baun (vilt líklega hlusta frekar eftir coupon, þurfum þá coupon klasa og henda honum á orderinfo og orderline og breyta "öllu")
                        //line.Discount?.OnCouponApply();
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex) // Swallow all event subscriber exceptions
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        _logger.Error<CheckoutService>(ex);
                    }

                }

                await _orderService.ChangeOrderStatusAsync(o.UniqueId, OrderStatus.ReadyForDispatch)
                    .ConfigureAwait(false);

            }
            catch (StockException)
            {
                _logger.Info<CheckoutService>($"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}. "
                    + $"Order id: {oi?.UniqueId}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error<CheckoutService>(ex);
                throw;
            }
        }
    }
}
