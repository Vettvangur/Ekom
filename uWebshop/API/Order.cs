using System;
using System.Collections.Generic;
using System.Web.Mvc;
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
                return _current ?? (_current = Configuration.container.GetService<Order>());
            }
        }

        IOrderService _orderService;
        IStoreService _storeSvc;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="orderService"></param>
        /// <param name="storeSvc"></param>
        public Order(IOrderService orderService, IStoreService storeSvc)
        {
            _orderService = orderService;
            _storeSvc = storeSvc;
        }

        /// <summary>
        /// Get order using cookie data and uwbsRequest store.
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
