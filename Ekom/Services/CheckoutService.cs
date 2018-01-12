using Ekom.Interfaces;
using Ekom.Models;
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

                if (oi.discount != null)
                {
                    _discountStockRepo.Update(oi.discount.Key.ToString(), -1);
                    oi.discount.OnCouponApply();
                }

                foreach (var line in oi.OrderLines.Where(line => line.discount != null))
                {
                    var id = $"{line.discount.Key}"
                    _discountStockRepo.Update()
                    line.discount.OnCouponApply();
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
