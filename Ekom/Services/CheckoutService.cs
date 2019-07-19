using Ekom.API;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Data;
using Ekom.Repository;
using log4net;
using System;
using System.Linq;
using Ekom.Helpers;

namespace Ekom.Services
{
    class CheckoutService
    {
        ILogger _logger;
        Configuration _config;
        IDiscountStockRepository _discountStockRepo;
        OrderRepository _orderRepo;
        ICouponRepository _couponRepo;
        private OrderService _orderService;
        public CheckoutService(
            ILogger logger,
            Configuration config,
            OrderRepository orderRepo,
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

        public void Complete(Guid key)
        {
            OrderData o = null;
            OrderInfo oi = null;

            try
            {
                o = _orderRepo.GetOrder(key);

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
                            _couponRepo.MarkUsed(oi.Coupon);
                        }
                    }
                    catch (Exception ex) // Swallow all event subscriber exceptions
                    {
                        _log.Error(ex);
                    }
                }

                foreach (var line in oi.OrderLines.Where(line => line.Discount != null))
                {
                    if (!string.IsNullOrEmpty(line.Coupon))
                    {
                        var id = $"{line.Discount.Key}_{line.Coupon}";
                        _discountStockRepo.Update(id, -1);
                    }

                    if (line.Discount.HasMasterStock)
                    {
                        _discountStockRepo.Update(line.Discount.Key.ToString(), -1);
                    }

                    try
                    {
                        //discount eventar virka ekki baun (vilt líklega hlusta frekar eftir coupon, þurfum þá coupon klasa og henda honum á orderinfo og orderline og breyta "öllu")
                        //line.Discount?.OnCouponApply();
                    }
                    catch (Exception ex) // Swallow all event subscriber exceptions
                    {
                        _log.Error(ex);
                    }

                }

                _orderService.ChangeOrderStatus(o.UniqueId, OrderStatus.ReadyForDispatch);

            }
            catch (StockException)
            {
                _log.Info($"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}. "
                    + $"Order id: {oi?.UniqueId}");
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                throw;
            }
        }
    }
}
