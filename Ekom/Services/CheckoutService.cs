using Ekom.API;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Data;
using Ekom.Models.Discounts;
using Ekom.Repository;
using log4net;
using System;
using System.Linq;
using Umbraco.NetPayment;

namespace Ekom.Services
{
    class CheckoutService
    {
        ILog _log;
        Configuration _config;
        IDiscountStockRepository _discountStockRepo;
        OrderRepository _orderRepo;
        public CheckoutService(
            ILogFactory logFac,
            Configuration config,
            OrderRepository orderRepo,
            IDiscountStockRepository discountStockRepo)
        {
            _log = logFac.GetLogger<CheckoutService>();
            _config = config;
            _orderRepo = orderRepo;
            _discountStockRepo = discountStockRepo;
        }

        public void Complete(OrderStatus orderStatus)
        {
            OrderData o = null;
            OrderInfo oi = null;
            try
            {
                var orderKey = Guid.Parse(orderStatus.Custom);
                o = _orderRepo.GetOrder(orderKey);

                oi = new OrderInfo(o);

                foreach (var job in oi.HangfireJobs)
                {
                    Stock.Current.CancelRollback(job);
                }

                if (oi.Discount != null)
                {
                    try
                    {
                        (oi.Discount as Discount)?.OnCouponApply();
                    }
                    catch (Exception ex) // Swallow all event subscriber exceptions
                    {
                        _log.Error(ex);
                    }
                }

                foreach (var line in oi.OrderLines.Where(line => line.Discount != null))
                {
                    var id = $"{line.Discount.Key}_{line.Coupon}";
                    _discountStockRepo.Update(id, -1);

                    if (line.Discount.HasMasterStock)
                    {
                        _discountStockRepo.Update(line.Discount.Key.ToString(), -1);
                    }

                    try
                    {
                        (line.Discount as Discount)?.OnCouponApply();
                    }
                    catch (Exception ex) // Swallow all event subscriber exceptions
                    {
                        _log.Error(ex);
                    }
                }
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
