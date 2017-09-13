using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using uWebshop.App_Start;
using uWebshop.Helpers;
using uWebshop.Interfaces;
using uWebshop.Models;

namespace uWebshop.API
{
    /// <summary>
    /// The uWebshop API, get/update/remove operations on orders 
    /// </summary>
    public class Order
    {
        private static Order _current;
        /// <summary>
        /// Order Singleton
        /// </summary>
        public static Order Current
        {
            get
            {
                return _current ?? (_current = UnityConfig.GetConfiguredContainer().Resolve<Order>());
            }
        }

        IOrderService _orderService;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="orderService"></param>
        public Order(IOrderService orderService)
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
