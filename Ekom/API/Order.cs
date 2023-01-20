using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public static Order Instance => Configuration.Resolver.GetService<Order>();

        #region Events

        /// <summary>
        /// Event to fire on <see cref="IOrderInfo"/> updates
        /// </summary>
        public static event EventHandler<OrderUpdatedEventArgs> OrderUpdated;
        internal static void OnOrderUpdated(object sender, OrderUpdatedEventArgs args)
            => OrderUpdated?.Invoke(sender, args);

        public static event EventHandler<OrderUpdatingEventArgs> OrderUpdateing;
        internal static void OnOrderUpdateing(object sender, OrderUpdatingEventArgs args)
            => OrderUpdateing?.Invoke(sender, args);

        public static event EventHandler<OrderStatusEventArgs> OrderStatusChanging;
        internal static void OnOrderStatusChanging(object sender, OrderStatusEventArgs args)
            => OrderStatusChanging?.Invoke(sender, args);
        public static event EventHandler<OrderStatusEventArgs> OrderStatusChanged;
        internal static void OnOrderStatusChanged(object sender, OrderStatusEventArgs args)
            => OrderStatusChanged?.Invoke(sender, args);

        #endregion

        readonly ILogger<Order> _logger;
        readonly Configuration _config;
        readonly DiscountCache _discountCache;
        readonly ICouponCache _couponCache;
        readonly OrderService _orderService;
        readonly CheckoutService _checkoutService;
        readonly IStoreService _storeSvc;

        /// <summary>
        /// ctor
        /// </summary>
        internal Order(
            Configuration config,
            ILogger<Order> logger,
            DiscountCache discountCache,
            ICouponCache couponCache,
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
            _logger = logger;
        }

        public IOrderInfo GetOrder() => GetOrderAsync().Result;

        /// <summary>
        /// Get order using cookie data and ekmRequest store.
        /// Retrieves from session if possible, otherwise from SQL.
        /// </summary>
        /// <returns></returns>
        public Task<IOrderInfo> GetOrderAsync()
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store == null)
            {
                return Task.FromResult<IOrderInfo>(null);
            }

            return GetOrderAsync(store.Alias);
        }

        public IOrderInfo GetOrder(string storeAlias) => GetOrderAsync(storeAlias).Result;

        /// <summary>
        /// Get order using cookie data and provided store.
        /// Retrieves from session if possible, otherwise from SQL.
        /// </summary>
        /// <returns></returns>
        public async Task<IOrderInfo> GetOrderAsync(string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
            }

            return await _orderService.GetOrderAsync(storeAlias).ConfigureAwait(false);
        }

        /// <summary>
        /// Get order regardless of status using <see cref="Guid"/>.
        /// This may return complete / final orders. <br />
        /// Do not use for cart or checkout display.
        /// </summary>
        /// <returns></returns>
        public IOrderInfo GetOrder(Guid uniqueId) => _orderService.GetOrderAsync(uniqueId).Result;

        /// <summary>
        /// Get completed order using cookie data and provided store.
        /// Retrieves from session if possible, otherwise from SQL.
        /// </summary>
        /// <returns></returns>
        public IOrderInfo GetCompletedOrder(string storeAlias) => GetCompletedOrderAsync(storeAlias).Result;

        /// <summary>
        /// Get completed order using cookie data and provided store.
        /// Retrieves from session if possible, otherwise from SQL.
        /// </summary>
        /// <returns></returns>
        public async Task<IOrderInfo> GetCompletedOrderAsync(string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException($"The value for '{nameof(storeAlias)}' must be specified");
            }

            return await _orderService.GetCompletedOrderAsync(storeAlias)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Get all orders with the given <see cref="OrderStatus"/> for the given customerUsername
        /// </summary>
        /// <param name="customerUsername"></param>
        /// <param name="orderStatuses"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IOrderInfo>> GetStatusOrdersByCustomerUsernameAsync(
            string customerUsername,
            params OrderStatus[] orderStatuses
        )
        {
            if (string.IsNullOrEmpty(customerUsername))
            {
                throw new ArgumentException($"Null or empty {nameof(customerUsername)} provided");
            }
            if (!orderStatuses.Any())
            {
                throw new ArgumentException("At least one OrderStatus must be specified");
            }

            var orders = await _orderService.GetStatusOrdersByCustomerUsernameAsync(customerUsername, orderStatuses)
                .ConfigureAwait(false);

            return orders.Cast<IOrderInfo>();
        }

        /// <summary>
        /// Get all orders with the given <see cref="OrderStatus"/> for the given customer
        /// </summary>
        /// <param name="customerId"></param>
        /// <param name="orderStatuses"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IOrderInfo>> GetStatusOrdersByCustomerIdAsync(
            int customerId,
            params OrderStatus[] orderStatuses
        )
        {
            if (!orderStatuses.Any())
            {
                throw new ArgumentException("At least one OrderStatus must be specified");
            }

            var orders = await _orderService.GetStatusOrdersByCustomerIdAsync(customerId, orderStatuses)
                .ConfigureAwait(false);

            return orders.Cast<IOrderInfo>();
        }

        /// <summary>
        /// Get all orders for logged in user with the given <see cref="OrderStatus"/>
        /// </summary>
        /// <param name="orderStatuses"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IOrderInfo>> GetStatusOrdersByCustomerIdAsync(
            params OrderStatus[] orderStatuses
        )
        {
            if (!orderStatuses.Any())
            {
                throw new ArgumentException("At least one OrderStatus must be specified");
            }

            var orders = await _orderService.GetStatusOrdersByCustomerIdAsync(orderStatuses)
                .ConfigureAwait(false);

            return orders.Cast<IOrderInfo>();
        }

        /// <summary>
        /// Get all orders with the given <see cref="OrderStatus"/>
        /// </summary>
        /// <param name="orderStatuses"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IOrderInfo>> GetStatusOrdersAsync(
            params OrderStatus[] orderStatuses
        )
        {
            if (!orderStatuses.Any())
            {
                throw new ArgumentException("At least one OrderStatus must be specified");
            }

            var orders = await _orderService.GetStatusOrdersAsync(orderStatuses)
                .ConfigureAwait(false);

            return orders.Cast<IOrderInfo>();
        }


        /// <summary>
        /// Completed orders with <see cref="OrderStatus"/> in one of the last stages
        /// </summary>
        /// <param name="customerId"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IOrderInfo>> GetCompleteCustomerOrdersAsync(int customerId)
        {
            return await _orderService.GetCompleteCustomerOrdersAsync(customerId)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OrderInfoNotFoundException"></exception>
        public async Task UpdateStatusAsync(string storeAlias, OrderStatus newStatus, ChangeOrderSettings settings = null)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
            }

            OrderInfo orderInfo;
            if (settings?.OrderInfo == null)
            {
                orderInfo = await _orderService.GetOrderAsync(storeAlias).ConfigureAwait(false);
            }
            else
            {
                orderInfo = settings.OrderInfo as OrderInfo;
            }

            if (orderInfo == null)
            {
                throw new OrderInfoNotFoundException();
            }

            await _orderService.ChangeOrderStatusAsync(orderInfo.UniqueId, newStatus, null, settings)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderInfoNotFoundException">OrderData not found for given OrderId</exception>
        public async Task UpdateStatusAsync(OrderStatus newStatus, Guid orderId, string userName = null, ChangeOrderSettings settings = null)
        {
            await _orderService.ChangeOrderStatusAsync(orderId, newStatus, userName, settings)
                .ConfigureAwait(false);
        }

        public Task<IOrderInfo> UpdateCurrencyAsync(string currency, Guid orderId, string storeAlias)
        {
            return _orderService.ChangeCurrencyAsync(orderId, currency, storeAlias);
        }

        /// <summary>
        /// Add order line to cart asynchronously.
        /// </summary>
        /// <param name="productId">The product identifier.</param>
        /// <param name="quantity">The quantity.</param>
        /// <param name="storeAlias">The store alias.</param>
        /// <param name="settings">Ekom Order Api AddOrderLine optional configuration</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">storeAlias</exception>
        /// <exception cref="OrderLineNegativeException">Can indicate a request to modify lines to negative values f.x. </exception>
        /// <exception cref="ProductNotFoundException"></exception>
        /// <exception cref="VariantNotFoundException"></exception>
        /// <exception cref="NotEnoughStockException"></exception>
        public async Task<IOrderInfo> AddOrderLineAsync(
            Guid productId,
            int quantity,
            string storeAlias,
            AddOrderSettings settings = null
        )
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                var store = _storeSvc.GetStoreFromCache();

                if (store != null)
                {
                    storeAlias = store.Alias;
                }
            }
      
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
            }

            return await _orderService.AddOrderLineAsync(
                productId,
                quantity,
                storeAlias,
                settings: settings ?? new AddOrderSettings())
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task<IOrderInfo> UpdateCustomerInformationAsync(
            Dictionary<string, string> form,
            OrderSettings settings = null)
        {
            if (form == null)
            {
                throw new ArgumentNullException(nameof(form));
            }

            return await _orderService.UpdateCustomerInformationAsync(form, settings ?? null)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task<IOrderInfo> UpdateShippingInformationAsync(
            Guid shippingProvider,
            string storeAlias,
            OrderSettings settings = null)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
            }

            return await _orderService.UpdateShippingInformationAsync(shippingProvider, storeAlias, settings)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task<IOrderInfo> UpdatePaymentInformationAsync(
            Guid paymentProvider,
            string storeAlias,
            OrderSettings settings = null)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
            }

            return await _orderService.UpdatePaymentInformationAsync(paymentProvider, storeAlias, settings)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="productKey"></param>
        /// <param name="storeAlias">The store alias.</param>
        /// <param name="settings">Ekom Order Api optional configuration</param>
        /// <returns></returns>
        public async Task<IOrderInfo> RemoveOrderLineProductAsync(
            Guid productKey,
            string storeAlias,
            RemoveOrderSettings settings = null)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
            }

            return await _orderService.RemoveOrderLineProductAsync(productKey, storeAlias, settings ?? null)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lineId"></param>
        /// <param name="storeAlias">The store alias.</param>
        /// <param name="settings">Ekom Order Api optional configuration</param>
        /// <returns></returns>
        public async Task<IOrderInfo> RemoveOrderLineAsync(
            Guid lineId,
            string storeAlias,
            OrderSettings settings = null)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
            }

            return await _orderService.RemoveOrderLineAsync(lineId, storeAlias, settings)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Sets quantity to specified amount
        /// </summary>
        /// <param name="lineId"></param>
        /// <param name="quantity"></param>
        /// <param name="storeAlias"></param>
        /// <param name="settings"></param>
        /// <exception cref="OrderInfoNotFoundException"></exception>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="NotEnoughStockException"></exception>
        /// <returns></returns>
        public async Task<IOrderInfo> UpdateOrderlineQuantityAsync(
            Guid lineId,
            int quantity,
            string storeAlias,
            OrderSettings settings = null)
        {
            return await _orderService.UpdateOrderLineQuantityAsync(lineId, quantity, storeAlias, settings)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task CompleteOrderAsync(Guid orderId)
        {
            await _checkoutService.CompleteAsync(orderId)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Save multiple hangfire job ids to <see cref="IOrderInfo"/> and db
        /// </summary>
        /// <param name="hangfireJobs">Job IDs to add</param>
        public async Task AddHangfireJobsToOrderAsync(IEnumerable<string> hangfireJobs)
        {
            if (hangfireJobs == null)
            {
                throw new ArgumentNullException(nameof(hangfireJobs));
            }

            var store = _storeSvc.GetStoreFromCache();
            await AddHangfireJobsToOrderAsync(store.Alias, hangfireJobs)
                .ConfigureAwait(false);
        }
        /// <summary>
        /// Save multiple hangfire job ids to <see cref="IOrderInfo"/> and db
        /// </summary>
        /// <param name="storeAlias"></param>
        /// <param name="hangfireJobs">Job IDs to add</param>
        public async Task AddHangfireJobsToOrderAsync(string storeAlias, IEnumerable<string> hangfireJobs)
        {
            if (hangfireJobs == null)
            {
                throw new ArgumentNullException(nameof(hangfireJobs));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
            }

            await _orderService.AddHangfireJobsToOrderAsync(storeAlias, hangfireJobs)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Remove all hangfire job ids to <see cref="IOrderInfo"/> and db
        /// </summary>
        /// <param name="storeAlias"></param>
        public async Task RemoveHangfireJobsFromOrderAsync(string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("string.IsNullOrEmpty", nameof(storeAlias));
            }

            await _orderService.RemoveHangfireJobsToOrderAsync(storeAlias)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Determine if an <see cref="OrderStatus"/> is considered by Ekom as final.
        /// Ekom also uses this logic to control cart reset.
        /// </summary>
        /// <param name="orderStatus"></param>
        /// <returns></returns>
        public static bool IsOrderFinal(OrderStatus? orderStatus)
            => orderStatus != null
                && (orderStatus.Value == OrderStatus.OfflinePayment
                || orderStatus.Value == OrderStatus.Pending
                || orderStatus.Value == OrderStatus.ReadyForDispatch
                || orderStatus.Value == OrderStatus.ReadyForDispatchWhenStockArrives
                || orderStatus.Value == OrderStatus.Dispatched
                || orderStatus.Value == OrderStatus.WaitingForPayment
                || orderStatus.Value == OrderStatus.Returned
                || orderStatus.Value == OrderStatus.Cancelled
                || orderStatus.Value == OrderStatus.Closed
                );
    }
}
