using Ekom.Cache;
using Ekom.Helpers;
using Ekom.Interfaces;
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
        /// <summary>
        /// Order Instance
        /// </summary>
        public static Order Instance => Configuration.container.GetInstance<Order>();

        ILog _log;
        Configuration _config;
        DiscountCache _discountCache;
        CouponCache _couponCache;
        OrderService _orderService;
        CheckoutService _checkoutService;
        IStoreService _storeSvc;

        /// <summary>
        /// ctor
        /// </summary>
        internal Order(
            Configuration config,
            ILogFactory logFac,
            DiscountCache discountCache,
            CouponCache couponCache,
            OrderService orderService,
            CheckoutService checkoutService,
            IStoreService storeService
        )
        {
            _discountCache = discountCache;
            _orderService = orderService;
            _checkoutService = checkoutService;
            _storeSvc = storeService;
            _couponCache = couponCache;
            _config = config;
            _log = logFac.GetLogger<Order>();
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

        /// <summary>
        /// Get order using <see cref="Guid"/>.
        /// </summary>
        /// <returns></returns>
        public IOrderInfo GetOrder(Guid uniqueId)
        {
            return _orderService.GetOrder(uniqueId);
        }

        /// <summary>
        /// Get completed order using cookie data and provided store.
        /// Retrieves from session if possible, otherwise from SQL.
        /// </summary>
        /// <returns></returns>
        public IOrderInfo GetCompletedOrder(string storeAlias)
        {
            return _orderService.GetCompletedOrder(storeAlias);
        }

        public void UpdateStatus(string storeAlias, OrderStatus newStatus)
        {
            var order = _orderService.GetOrder(storeAlias);
            _orderService.ChangeOrderStatus(order.UniqueId, newStatus);
        }

        public void UpdateStatus(OrderStatus newStatus, Guid orderId, string userName = null)
        {
            _orderService.ChangeOrderStatus(orderId, newStatus, userName);
        }

        public IOrderInfo AddOrderLine(
            Guid productId,
            int quantity,
            string storeAlias,
            OrderAction? action = null,
            Guid? variantId = null
        )
        {
            return _orderService.AddOrderLine(productId, quantity, storeAlias, action, variantId);
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

        public void CompleteOrder(Guid orderId)
        {
            _checkoutService.Complete(orderId);
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
