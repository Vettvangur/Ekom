using Ekom.Cache;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.Discounts;
using Ekom.Services;
using log4net;
using System;
using System.Collections.Generic;

namespace Ekom.API
{
    /// <summary>
    /// The Ekom API, get/update/remove operations on orders 
    /// </summary>
    public partial class Order
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

        OrderService _orderService => Configuration.container.GetInstance<OrderService>();
        IStoreService _storeSvc => Configuration.container.GetInstance<IStoreService>();
        DiscountCache _discountCache => Configuration.container.GetInstance<DiscountCache>();

        ILog _log;
        Configuration _config;

        /// <summary>
        /// ctor
        /// </summary>
        public Order(ILogFactory logFac, Configuration config)
        {
            _log = logFac.GetLogger<Order>();
            _config = config;
        }

        /// <summary>
        /// Get order using cookie data and ekmRequest store.
        /// Retrieves from session if possible, otherwise from SQL.
        /// </summary>
        /// <returns></returns>
        public IOrderInfo GetOrder()
        {
            var store = _storeSvc.GetStoreFromCache();
            return GetOrder(store.Alias);
        }

        /// <summary>
        /// Get order using cookie data and provided store.
        /// Retrieves from session if possible, otherwise from SQL.
        /// </summary>
        /// <returns></returns>
        public IOrderInfo GetOrder(string storeAlias)
        {
            return _orderService.GetOrder(storeAlias);
        }
        public void UpdateStatus(string storeAlias, OrderStatus newStatus)
        {
            var order = _orderService.GetOrder(storeAlias);
            _orderService.ChangeOrderStatus(order.UniqueId, newStatus);
        }

        public void UpdateStatus(string storeAlias, OrderStatus newStatus, Guid orderId)
        {
            _orderService.ChangeOrderStatus(orderId, newStatus);
        }

        public IOrderInfo AddOrderLine(
            Guid productId,
            IEnumerable<Guid> variantIds,
            int quantity,
            string storeAlias,
            OrderAction? action
        )
        {
            return _orderService.AddOrderLine(productId, variantIds, quantity, storeAlias, action);
        }

        public IOrderInfo UpdateCustomerInformation(Dictionary<string, string> form)
        {
            return _orderService.UpdateCustomerInformation(form);
        }

        public IOrderInfo UpdateShippingInformation(Guid ShippingProvider, string storeAlias)
        {
            return _orderService.UpdateShippingInformation(ShippingProvider, storeAlias);
        }

        public IOrderInfo UpdatePaymentInformation(Guid PaymentProvider, string storeAlias)
        {
            return _orderService.UpdatePaymentInformation(PaymentProvider, storeAlias);
        }

        public IOrderInfo RemoveOrderLine(Guid lineId, string storeAlias)
        {
            return _orderService.RemoveOrderLine(lineId, storeAlias);
        }

        /// <summary>
        /// Completed orders with <see cref="OrderStatus"/> in one of the last stages
        /// </summary>
        /// <param name="customerId"></param>
        /// <returns></returns>
        public IEnumerable<IOrderInfo> GetCompleteCustomerOrders(int customerId)
        {
            return _orderService.GetCompleteCustomerOrders(customerId);
        }

        /// <summary>
        /// Save multiple hangfire job ids to <see cref="IOrderInfo"/> and db
        /// </summary>
        /// <param name="hangfireJobs">Job IDs to add</param>
        public void AddHangfireJobsToOrder(IEnumerable<string> hangfireJobs)
        {
            var store = _storeSvc.GetStoreFromCache();
            AddHangfireJobsToOrder(store.Alias, hangfireJobs);
        }
        /// <summary>
        /// Save multiple hangfire job ids to <see cref="IOrderInfo"/> and db
        /// </summary>
        /// <param name="storeAlias"></param>
        /// <param name="hangfireJobs">Job IDs to add</param>
        public void AddHangfireJobsToOrder(string storeAlias, IEnumerable<string> hangfireJobs)
        {
            var store = _storeSvc.GetStoreFromCache();
            _orderService.AddHangfireJobsToOrder(store.Alias, hangfireJobs);
        }
    }
}
