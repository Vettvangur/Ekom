using Ekom.API;
using Ekom.Interfaces;
using Ekom.Models;
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
            try
            {
                var orderKey = Guid.Parse(orderStatus.Custom);
                var o = _orderRepo.GetOrder(orderKey);

                var oi = new OrderInfo(o);

                if (oi.Discount != null)
                {
                    try
                    {
                        //Stock.Current.CancelRollback()

                    }
                    catch { } // unfinished
                    try
                    {
                        (oi.Discount as Discount).OnCouponApply();
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
                        (line.Discount as Discount).OnCouponApply();
                    }
                    catch (Exception ex) // Swallow all event subscriber exceptions
                    {
                        _log.Error(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                throw;
            }
        }
    }
}
