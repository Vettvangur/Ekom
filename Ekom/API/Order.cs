using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models;
using System;
using System.Collections.Generic;

namespace Ekom.API
{
    /// <summary>
    /// The Ekom API, get/update/remove operations on orders 
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
                return _current ?? (_current = Configuration.container.GetInstance<Order>());
            }
        }

        IOrderService _orderService => Configuration.container.GetInstance<IOrderService>();
        IStoreService _storeSvc => Configuration.container.GetInstance<IStoreService>();

        /// <summary>
        /// Get order using cookie data and ekmRequest store.
        /// Retrieves from session if possible, otherwise from SQL.
        /// </summary>
        /// <returns></returns>
        public OrderInfo GetOrder()
        {
            var store = _storeSvc.GetStoreFromCache();
            return GetOrder(store.Alias);
        }

        /// <summary>
        /// Get order using cookie data and provided store.
        /// Retrieves from session if possible, otherwise from SQL.
        /// </summary>
        /// <returns></returns>
        public OrderInfo GetOrder(string storeAlias)
        {
            return _orderService.GetOrder(storeAlias);
        }
        public void UpdateStatus(string storeAlias, OrderStatus newStatus)
        {
            var order = _orderService.GetOrder(storeAlias);
            _orderService.ChangeOrderStatus(order.UniqueId, newStatus);
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
