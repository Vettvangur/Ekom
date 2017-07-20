using log4net;
using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using uWebshop.App_Start;
using uWebshop.Helpers;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.API
{
    public class Order
    {
        private static Order _instance;
        public static Order Instance
        {
            get
            {
                return _instance ?? (_instance = UnityConfig.GetConfiguredContainer().Resolve<Order>());
            }
        }

        OrderService _orderService;

        public Order(OrderService orderService)
        {
            _orderService = orderService;
        }

        public OrderInfo GetOrder(string storeAlias)
        {
            return _orderService.GetOrder(storeAlias);
        }

        public OrderInfo AddOrderLine(
            Guid productId, 
            IEnumerable<Guid> variantIds, 
            int quantity, 
            string storeAlias, 
            OrderAction? action
        )
        {
            return _orderService.AddOrderLine(productId, variantIds, quantity, storeAlias, action);
        }

        public OrderInfo RemoveOrderLine(Guid lineId, string storeAlias)
        {
            return _orderService.RemoveOrderLine(lineId, storeAlias);
        }
    }
}
