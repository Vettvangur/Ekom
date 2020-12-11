using Ekom.API;
using Ekom.API.Settings;
using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.JsonDotNet;
using Ekom.Models;
using Ekom.Models.Data;
using Ekom.Models.Events;
using Ekom.Models.OrderedObjects;
using Ekom.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Services;

namespace Ekom.Services
{
    partial class OrderService
    {
        readonly ILogger _logger;
        readonly HttpContextBase _httpCtx;
        readonly IAppCache _reqCache;
        readonly IAppPolicyCache _runtimeCache;
        readonly IMemberService _memberService;
        readonly DiscountCache _discountCache;
        readonly IActivityLogRepository _activityLogRepository;
        readonly IOrderRepository _orderRepository;
        readonly ICouponRepository _couponRepository;
        readonly IStoreService _storeSvc;
        readonly ContentRequest _ekmRequest;

        // ToDo: Refactor, this is too error prone. There is a reason nobody does it like this.
        // Prefer supply these as params
        IStore _store;
        DateTime _date;

        /// <summary>
        /// W/o HttpCtx, for usage in Hangfire f.x. ?
        /// </summary>
        public OrderService(
            IOrderRepository orderRepo,
            ICouponRepository couponRepository,
            IActivityLogRepository activityLogRepository,
            ILogger logger,
            IStoreService storeService,
            AppCaches appCaches,
            IMemberService memberService,
            DiscountCache discountCache)
        {
            _logger = logger;

            _orderRepository = orderRepo;
            _couponRepository = couponRepository;
            _activityLogRepository = activityLogRepository;
            _storeSvc = storeService;
            _discountCache = discountCache;
            _reqCache = appCaches.RequestCache;
            _runtimeCache = appCaches.RuntimeCache;
            _memberService = memberService;
        }

        /// <summary>
        /// ctor
        /// </summary>
        public OrderService(
            IOrderRepository orderRepo,
            ICouponRepository couponRepository,
            IActivityLogRepository activityLogRepository,
            ILogger logger,
            IStoreService storeService,
            AppCaches appCaches,
            IMemberService memberService,
            DiscountCache discountCache,
            HttpContextBase httpCtx)
            : this(orderRepo, couponRepository, activityLogRepository, logger, storeService, appCaches, memberService, discountCache)
        {
            _httpCtx = httpCtx;
            _ekmRequest = _reqCache.GetCacheItem<ContentRequest>("ekmRequest");
        }

        public OrderInfo GetOrder(string storeAlias)
        {
            var store = _storeSvc.GetStoreByAlias(storeAlias);

            return GetOrder(store);
        }

        public OrderInfo GetOrder(IStore store)
        {
            if (Configuration.Current.UserBasket)
            {
                if (_ekmRequest.User != null && !string.IsNullOrEmpty(_ekmRequest.User.Username))
                {
                    var orderInfo = GetOrder(_ekmRequest.User.OrderId);
                    if (orderInfo?.OrderStatus != OrderStatus.ReadyForDispatch
                    && orderInfo?.OrderStatus != OrderStatus.Dispatched)
                    {
                        return orderInfo;
                    }
                }
            }
            else
            {
                _store = store;

                var key = CreateKey(store.Alias);
                // Get Cart UniqueId from Cookie.
                var orderUniqueId = GetOrderIdFromCookie(key);

                // If Cookie Exist then return Cart
                if (orderUniqueId != Guid.Empty)
                {
                    var orderInfo = _runtimeCache.GetCacheItem(
                        orderUniqueId.ToString(),
                        () => GetOrder(orderUniqueId),
                        TimeSpan.FromDays(1));

                    //// If the cart is not in the session, fetch order from sql and insert to session
                    //if (ApplicationContext.Current.ApplicationCache.RuntimeCache.GetCacheItem(key) == null)
                    //{
                    //    _log.Debug("Order is not in the session. Creating from sql");

                    //    var order = GetOrder(orderUniqueId);

                    //    ApplicationContext.Current.ApplicationCache.RuntimeCache.InsertCacheItem<OrderInfo>(key,) = order;
                    //}

                    //var orderInfo = (OrderInfo)_httpCtx.Session[key];

                    if (orderInfo?.OrderStatus != OrderStatus.ReadyForDispatch
                    && orderInfo?.OrderStatus != OrderStatus.Dispatched 
                    && orderInfo?.OrderStatus != OrderStatus.OfflinePayment)
                    {
                        return orderInfo;
                    }
                }
            }

            return null;
        }

        public Task<OrderInfo> GetCompletedOrderAsync(string storeAlias)
        {
            // Add timelimit to get the order ? Maybe 1-2 hours ?

            var store = _storeSvc.GetStoreByAlias(storeAlias);

            if (Configuration.Current.UserBasket)
            {
                if (_ekmRequest.User != null && !string.IsNullOrEmpty(_ekmRequest.User.Username))
                {
                    var orderInfo = GetOrder(_ekmRequest.User.OrderId);

                    if (orderInfo?.OrderStatus == OrderStatus.ReadyForDispatch
                    || orderInfo?.OrderStatus == OrderStatus.Dispatched
                    || orderInfo?.OrderStatus == OrderStatus.OfflinePayment
                    || orderInfo?.OrderStatus == OrderStatus.Pending)
                    {
                        return Task.FromResult(orderInfo);
                    }
                }
            }
            else
            {
                _store = store;

                var key = CreateKey(storeAlias);
                // Get Cart UniqueId from Cookie.
                var orderUniqueId = GetOrderIdFromCookie(key);

                // If Cookie Exist then return Cart
                if (orderUniqueId != Guid.Empty)
                {
                    var orderInfo = GetOrder(orderUniqueId);

                    if (orderInfo?.OrderStatus == OrderStatus.ReadyForDispatch
                    || orderInfo?.OrderStatus == OrderStatus.Dispatched
                    || orderInfo?.OrderStatus == OrderStatus.OfflinePayment
                    || orderInfo?.OrderStatus == OrderStatus.Pending)
                    {
                        return Task.FromResult(orderInfo);
                    }
                }
            }

            return null;
        }

        public OrderInfo GetOrder(Guid uniqueId)
        {
            // Check for cache ?
            return _runtimeCache.GetCacheItem(
                uniqueId.ToString(),
                () => GetOrderInfoAsync(uniqueId).Result, 
                TimeSpan.FromMinutes(5));
        }

        // fills cache for GetOrder
        public async Task<OrderInfo> GetOrderUpdateCacheAsync(Guid uniqueId)
        {
            return await GetOrderInfoAsync(uniqueId).ConfigureAwait(false);
        }

        public async Task<OrderInfo> GetOrderInfoAsync(Guid uniqueId)
        {
            var orderData = await _orderRepository.GetOrderAsync(uniqueId)
                .ConfigureAwait(false);

            return orderData != null ? new OrderInfo(orderData) : null;
        }


        public async Task ChangeOrderStatusAsync(
            Guid uniqueId,
            OrderStatus status,
            string userName = null,
            bool fireEvents = true)
        {
            var order = await _orderRepository.GetOrderAsync(uniqueId)
                .ConfigureAwait(false);

            var oldStatus = order.OrderStatus;

            if (fireEvents)
            {
                Order.OnOrderStatusChanging(this, new OrderStatusEventArgs
                {
                    OrderUniqueId = uniqueId,
                    PreviousStatus = oldStatus,
                    Status = status,
                });
            }

            order.OrderStatus = status;

            // Create function for this, For completed orders
            if (status == OrderStatus.ReadyForDispatch || status == OrderStatus.OfflinePayment)
            {
                if (status == OrderStatus.ReadyForDispatch && !order.PaidDate.HasValue)
                {
                    order.PaidDate = DateTime.Now;
                }

                if (Configuration.Current.UserBasket)
                {
                    var member = _memberService.GetByUsername(_httpCtx.User.Identity.Name);
                    if (member.HasProperty("orderId"))
                    {
                        member.SetValue("orderId", "");
                    }
                    _memberService.Save(member);
                }
                else
                {
                    _runtimeCache.ClearByKey(uniqueId.ToString());
                }
            }

            await _orderRepository.UpdateOrderAsync(order)
                .ConfigureAwait(false);

            _runtimeCache.GetCacheItem<OrderInfo>(
                uniqueId.ToString(),
                () => new OrderInfo(order),
                TimeSpan.FromDays(1));

            if (fireEvents)
            {
                Order.OnOrderStatusChanged(this, new OrderStatusEventArgs
                {
                    OrderUniqueId = uniqueId,
                    PreviousStatus = oldStatus,
                    Status = status,
                });
            }

            await _activityLogRepository.InsertAsync(
                uniqueId,
                $"Order status changed. From: {oldStatus.ToString()} To: {status.ToString()}",
                string.IsNullOrEmpty(userName)
                    ? "Customer"
                    : userName)
                .ConfigureAwait(false);

            _logger.Debug<OrderService>(
                "Change Order {OrderNumber} status to {Status}",
                order.OrderNumber,
                status);
        }

        public async Task<OrderInfo> UpdateOrderlineQuantity(
            Guid orderLineId,
            int quantity,
            string storeAlias
        )
        {
            var store = _storeSvc.GetStoreByAlias(storeAlias);

            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            var orderInfo = GetOrder(store);

            if (orderInfo == null)
            {
                throw new ArgumentNullException(nameof(orderInfo));
            }

            var orderline = orderInfo.orderLines.FirstOrDefault(x => x.Key == orderLineId);

            if (orderline == null)
            {
                throw new OrderLineNotFoundException("Could not find order line with key: " + orderLineId);
            }

            orderline.Quantity = quantity;

            await UpdateOrderAndOrderInfoAsync(orderInfo)
                .ConfigureAwait(false);

            return orderInfo;
        }

        public async Task ChangeCurrencyAsync(Guid uniqueId, string currency, string storeAlias)
        {
            var store = _storeSvc.GetStoreByAlias(storeAlias);

            var storeCurrency = store.Currencies.FirstOrDefault(x => x.CurrencyValue == currency);

            if (storeCurrency != null)
            {

                var order = await _orderRepository.GetOrderAsync(uniqueId).ConfigureAwait(false);

                var oldCurrency = order.Currency;

                order.Currency = storeCurrency.ISOCurrencySymbol;

                var orderInfo = GetOrder(uniqueId);

                if (orderInfo != null)
                {
                    orderInfo.StoreInfo.Currency = storeCurrency;

                    var serializedOrderInfo = JsonConvert.SerializeObject(orderInfo, EkomJsonDotNet.settings);

                    order.OrderInfo = serializedOrderInfo;

                    await _orderRepository.UpdateOrderAsync(order).ConfigureAwait(false);

                    _runtimeCache.GetCacheItem<OrderInfo>(
                        uniqueId.ToString(),
                        () => new OrderInfo(order),
                        TimeSpan.FromDays(1));

                    _logger.Debug<OrderService>(
                        "Change Currency {OldCurrency}  to {Currency}",
                        oldCurrency,
                        currency);
                }
            }

        }

        public async Task UpdatePaidDate(Guid uniqueId)
        {

            var order = await _orderRepository.GetOrderAsync(uniqueId)
                .ConfigureAwait(false);

            order.PaidDate = DateTime.Now;

            await _orderRepository.UpdateOrderAsync(order)
                .ConfigureAwait(false);

            _logger.Debug<OrderService>(
                "Update Paid Date {OrderNumber}",
                order.OrderNumber);
        }

        /// <summary>
        /// Add order line to cart asynchronously.
        /// </summary>
        /// <exception cref="ArgumentException">productKey</exception>
        /// <exception cref="OrderLineNegativeException">Can indicate a request to modify lines to negative values f.x. </exception>
        /// <exception cref="ProductNotFoundException"></exception>
        /// <exception cref="VariantNotFoundException"></exception>
        /// <exception cref="NotEnoughStockException"></exception>
        public async Task<OrderInfo> AddOrderLineAsync(
            Guid productKey,
            int quantity,
            string storeAlias,
            AddOrderSettings settings = null
        )
        {
            if (productKey == Guid.Empty)
            {
                throw new ArgumentException("Empty product key", nameof(productKey));
            }

            var product = Catalog.Instance.GetProduct(productKey);

            var disableStock = ConfigurationManager.AppSettings["ekmCustomDisableStock"];

            if (product == null)
            {
                throw new ProductNotFoundException("Unable to find product with key " + productKey);
            }
            else
            {
                if (settings?.VariantKey == null && !product.Backorder && product.Stock < quantity && (disableStock == "true" ? false : true))
                {
                    throw new NotEnoughStockException("Stock not available for product " + product.Key);
                }
            }

            IVariant variant = null;
            if (settings?.VariantKey != null)
            {
                variant = Catalog.Instance.GetVariant(settings.VariantKey.Value);

                if (variant == null)
                {
                    throw new VariantNotFoundException("Unable to find variant with key " + settings.VariantKey);
                }
                else
                {
                    if ((!product.Backorder && variant.Stock < quantity) 
                    && (disableStock == "true" ? false : true))
                    {
                        throw new NotEnoughStockException("Stock not available for variant " + settings.VariantKey);
                    }
                }
            }

            var store = _storeSvc.GetStoreByAlias(storeAlias);

            return await AddOrderLineAsync(
                product,
                quantity,
                store,
                settings?.OrderAction,
                variant,
                settings?.FireOnOrderUpdatedEvent ?? true
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OrderLineNegativeException">Can indicate a request to modify lines to negative values f.x. </exception>
        public async Task<OrderInfo> AddOrderLineAsync(
            IProduct product,
            int quantity,
            IStore store,
            OrderAction? action = null,
            IVariant variant = null,
            bool fireEvents = true
        )
        {
            if (product == null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            _store = store ?? throw new ArgumentNullException(nameof(store));
            _date = DateTime.Now;

            // If cart action is null then update is the default state
            var cartAction = action != null ? action.Value : OrderAction.AddOrUpdate;

            var orderInfo = GetOrder(_store);

            if (orderInfo == null)
            {
                orderInfo = await CreateEmptyOrderAsync(_store.Alias).ConfigureAwait(false);
            }

            _logger.Debug<OrderService>("ProductId: {ProductId}" +
                " variantId: {VariantId}" +
                " qty: {Quantity}" +
                " Action: {Action}" +
                " Order: {OrderNumber}" +
                " Store: {Store}" +
                " Cart action {CartAction}",
                product.Id,
                variant?.Key,
                quantity,
                action,
                orderInfo.OrderNumber,
                _store.Alias,
                cartAction
            );

            await AddOrderLineToOrderInfoAsync(
                orderInfo,
                product,
                quantity,
                cartAction,
                variant,
                fireEvents).ConfigureAwait(false);

            return orderInfo;
        }

        public async Task<OrderInfo> RemoveOrderLineAsync(
            Guid lineId,
            string storeAlias,
            OrderSettings settings = null)
        {
            _logger.Debug<OrderService>("Remove OrderLine... LineId: " + lineId);

            _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);

            var orderInfo = GetOrder(storeAlias);

            var orderLine = orderInfo.OrderLines.FirstOrDefault(x => x.Key == lineId);

            if (orderLine != null)
            {
                RemoveOrderLine(orderInfo, orderLine as OrderLine);
            }
            else
            {
                throw new OrderLineNotFoundException("Could not find order line with key: " + lineId);
            }

            await UpdateOrderAndOrderInfoAsync(orderInfo, settings?.FireOnOrderUpdatedEvent ?? true)
                .ConfigureAwait(false);

            return orderInfo;
        }

        private void RemoveOrderLine(OrderInfo orderInfo, OrderLine orderLine)
        {
            orderInfo.orderLines.Remove(orderLine);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OrderLineNegativeException"></exception>
        private async Task AddOrderLineToOrderInfoAsync(
            OrderInfo orderInfo,
            IProduct product,
            int quantity,
            OrderAction action,
            IVariant variant,
            bool fireEvents
        )
        {
            if (quantity == 0)
            {
                // Use remove orderline instead
                throw new ArgumentException("Quantity can not be 0", nameof(quantity));
            }
            if (action == OrderAction.Set && quantity <= 0)
            {
                throw new ArgumentException("Quantity can not be set to 0 or less", nameof(quantity));
            }

            var lineId = Guid.NewGuid();

            _logger.Debug<OrderService>(
                "Order: {OrderNumber} Product Key: {ProductKey} Variant: {VariantKey} Action: {Action}",
                orderInfo.OrderNumber,
                product.Key,
                variant?.Key,
                action);

            OrderLine existingOrderLine = null;

            if (orderInfo.OrderLines != null)
            {
                if (variant != null)
                {
                    existingOrderLine
                        = orderInfo.OrderLines
                            .FirstOrDefault(
                                x => x.Product.Key == product.Key
                                && x.Product.VariantGroups
                                    .Any(b => b.Variants.Any(z => z.Key == variant.Key)))
                            as OrderLine;
                }
                else
                {
                    existingOrderLine
                        = orderInfo.OrderLines.FirstOrDefault(x => x.Product.Key == product.Key)
                        as OrderLine;
                }
            }

            if (existingOrderLine != null)
            {
                _logger.Debug<OrderService>("AddOrderLineToOrderInfo: existingOrderLine Found");

                // Update orderline quantity with value
                if (action == OrderAction.Set)
                {
                    existingOrderLine.Quantity = quantity;
                }
                else
                {
                    if (existingOrderLine.Quantity + quantity < 0)
                    {
                        throw new OrderLineNegativeException("OrderLines cannot be updated to negative quantity");
                    }

                    existingOrderLine.Quantity += quantity;

                    // If the update action ends up setting quantity to zero we remove the order line
                    if (existingOrderLine.Quantity == 0)
                    {
                        RemoveOrderLine(orderInfo, existingOrderLine);
                    }
                }
            }
            else
            {
                if (quantity < 0)
                {
                    throw new OrderLineNegativeException("OrderLines cannot be created with negative quantity");
                }

                // Update orderline when adding product to orderline

                _logger.Debug<OrderService>("AddOrderLineToOrderInfo: existingOrderLine Not Found");

                var orderLine = new OrderLine(
                    product,
                    quantity,
                    lineId,
                    orderInfo,
                    variant
                );

                orderInfo.orderLines.Add(orderLine);
               
                if (product.ProductDiscount() != null
                // Make sure that the current OrderInfo discount, if there is one, is inclusive
                // Meaning you can apply this discount while having a seperate discount 
                // affecting other OrderLines
                && (orderInfo.Discount == null || orderInfo.Discount.Stackable))
                {
                    _logger.Debug<OrderService>(
                        "Discount {ProductDiscountKey} found on product, applying to OrderLine",
                        product.ProductDiscount().Key);
                    await ApplyDiscountToOrderLineAsync(
                        orderLine,
                        product.ProductDiscount(),
                        orderInfo
                    ).ConfigureAwait(false);
                }
            }

            await UpdateOrderAndOrderInfoAsync(orderInfo, fireEvents)
                .ConfigureAwait(false);
        }

        private async Task UpdateOrderAndOrderInfoAsync(OrderInfo orderInfo, bool fireEvents = true)
        {
            _logger.Debug<OrderService>("Update Order with new OrderInfo");

            VerifyProviders(orderInfo);
            VerifyDiscounts(orderInfo);

            orderInfo.CustomerInformation.CustomerIpAddress = _ekmRequest.IPAddress;

            var serializedOrderInfo = JsonConvert.SerializeObject(orderInfo, EkomJsonDotNet.settings);

            var orderData = await _orderRepository.GetOrderAsync(orderInfo.UniqueId)
                .ConfigureAwait(false);

            if (_ekmRequest.User != null && !string.IsNullOrEmpty(_ekmRequest.User.Username))
            {
                orderInfo.CustomerInformation.Customer.UserId = _ekmRequest.User.UserId;
                orderInfo.CustomerInformation.Customer.UserName = _ekmRequest.User.Username;
                orderData.CustomerUsername = _ekmRequest.User.Username;
                orderData.CustomerId = _ekmRequest.User.UserId;
            }

            orderData.CustomerEmail = orderInfo.CustomerInformation.Customer.Email;
            orderData.CustomerName = orderInfo.CustomerInformation.Customer.Name;

            orderData.ShippingCountry = orderInfo.CustomerInformation.Shipping.Country;

            orderData.OrderInfo = serializedOrderInfo;
            orderData.UpdateDate = DateTime.Now;
            orderData.TotalAmount = orderInfo.ChargedAmount.Value;

            //Backwards compatability for old currency storeinfo 
            try
            {
                var culture = new CultureInfo(orderInfo.StoreInfo.Currency.CurrencyValue);

                var ri = new RegionInfo(culture.LCID);
                orderData.Currency = ri.ISOCurrencySymbol;

            }
            catch (ArgumentException)
            {
                orderData.Currency = orderInfo.StoreInfo.Culture;
            }

            await _orderRepository.UpdateOrderAsync(orderData)
                .ConfigureAwait(false);
            UpdateOrderInfoInCache(orderInfo);

            if (fireEvents)
            {
                Order.OnOrderUpdated(this, new OrderUpdatedEventArgs
                {
                    OrderInfo = orderInfo,
                });
            }
        }

        /// <summary>
        /// Is this necessary?
        /// </summary>
        /// <param name="orderInfo"></param>
        private void UpdateOrderInfoInCache(OrderInfo orderInfo)
        {
            var key = CreateKey(orderInfo.StoreInfo.Alias);

            _runtimeCache.InsertCacheItem<OrderInfo>(
                orderInfo.UniqueId.ToString(),
                () => orderInfo,
                TimeSpan.FromDays(1));
        }

        public async Task AddHangfireJobsToOrderAsync(string storeAlias, IEnumerable<string> hangfireJobs)
        {
            var o = GetOrder(storeAlias);

            o._hangfireJobs.AddRange(hangfireJobs);

            await UpdateOrderAndOrderInfoAsync(o)
                .ConfigureAwait(false);
        }

        public async Task RemoveHangfireJobsToOrderAsync(string storeAlias)
        {
            var o = GetOrder(storeAlias);

            o._hangfireJobs.Clear();

            await UpdateOrderAndOrderInfoAsync(o)
                .ConfigureAwait(false);
        }

        private async Task<OrderInfo> CreateEmptyOrderAsync(string storeAlias)
        {
            _logger.Debug<OrderService>("Add OrderLine ...  Create Empty Order..");

            Guid orderUniqueId;
            if (Configuration.Current.UserBasket)
            {
                orderUniqueId = Guid.NewGuid();

                var member = _memberService.GetByUsername(_httpCtx.User.Identity.Name);
                if (member.HasProperty("orderId"))
                {
                    member.SetValue("orderId", orderUniqueId.ToString());
                }
                _memberService.Save(member);
            }
            else
            {
                orderUniqueId = CreateOrderIdCookie(CreateKey(storeAlias));
            }

            var orderdata = await SaveEmptyOrderDataAsync(orderUniqueId)
                .ConfigureAwait(false);

            return new OrderInfo(orderdata, _store);
        }
        private async Task<OrderData> SaveEmptyOrderDataAsync(Guid uniqueId)
        {
            _logger.Debug<OrderService>("Add OrderLine ...  Create OrderData.. Store: {Store}", _store.Alias);

            var orderData = new OrderData
            {
                UniqueId = uniqueId,
                CreateDate = _date,
                StoreAlias = _store.Alias,
                OrderStatus = OrderStatus.Incomplete,
                Currency = _store.Currency.ISOCurrencySymbol,
                UpdateDate = DateTime.Now
            };

            if (_ekmRequest.User != null && !string.IsNullOrEmpty(_ekmRequest.User.Username))
            {
                orderData.CustomerEmail = _ekmRequest.User.Email;
                orderData.CustomerUsername = _ekmRequest.User.Username;
                orderData.CustomerId = _ekmRequest.User.UserId;
                orderData.CustomerName = _ekmRequest.User.Name;
            }

            await _orderRepository.InsertOrderAsync(orderData)
                .ConfigureAwait(false);

            orderData.OrderNumber = GenerateOrderNumberTemplate(orderData.ReferenceId);
            await _orderRepository.UpdateOrderAsync(orderData)
                .ConfigureAwait(false);

            return orderData;
        }

        public async Task<OrderInfo> UpdateCustomerInformationAsync(Dictionary<string, string> form)
        {
            _logger.Debug<OrderService>("UpdateCustomerInformation...");

            if (form.ContainsKey("storeAlias"))
            {
                var storeAlias = form["storeAlias"];

                _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);
                _date = DateTime.Now;

                var orderInfo = GetOrder(storeAlias);

                if (form.ContainsKey("ShippingProvider"))
                {
                    if (Guid.TryParse(form["ShippingProvider"], out Guid _providerKey))
                    {
                        orderInfo = await UpdateShippingInformationAsync(_providerKey, storeAlias).ConfigureAwait(false);
                    }
                }

                if (form.ContainsKey("PaymentProvider"))
                {
                    if (Guid.TryParse(form["PaymentProvider"], out Guid _providerKey))
                    {
                        orderInfo = await UpdatePaymentInformationAsync(_providerKey, storeAlias).ConfigureAwait(false);
                    }
                }

                foreach (var key in form.Keys.Where(x => x.StartsWith("customer", StringComparison.InvariantCulture)))
                {
                    var value = form[key];
                    orderInfo.CustomerInformation.Customer.Properties[key] = value;
                }

                foreach (var key in form.Keys.Where(x => x.StartsWith("shipping", StringComparison.InvariantCulture)))
                {
                    var value = form[key];

                    orderInfo.CustomerInformation.Shipping.Properties[key] = value;
                }

                await UpdateOrderAndOrderInfoAsync(orderInfo)
                    .ConfigureAwait(false);

                return orderInfo;
            }

            throw new ArgumentException("storeAlias parameter missing from form", nameof(form));
        }

        public async Task<OrderInfo> UpdateShippingInformationAsync(Guid shippingProviderId, string storeAlias)
        {
            _logger.Debug<OrderService>("UpdateShippingInformation...");

            _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);
            _date = DateTime.Now;

            var orderInfo = GetOrder(storeAlias);

            if (shippingProviderId != Guid.Empty)
            {
                var provider = API.Providers.Instance.GetShippingProvider(shippingProviderId, _store);

                if (provider != null)
                {

                    var orderedShippingProvider = new OrderedShippingProvider(provider, orderInfo.StoreInfo);
                    
                    orderInfo.ShippingProvider = orderedShippingProvider;

                    await UpdateOrderAndOrderInfoAsync(orderInfo)
                        .ConfigureAwait(false);
                }

            }

            return orderInfo;
        }

        public async Task<OrderInfo> UpdatePaymentInformationAsync(Guid paymentProviderId, string storeAlias)
        {
            _logger.Debug<OrderService>("UpdatePaymentInformation...");

            _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);
            _date = DateTime.Now;

            var orderInfo = GetOrder(storeAlias);

            if (paymentProviderId != Guid.Empty)
            {
                var provider = API.Providers.Instance.GetPaymentProvider(paymentProviderId, _store);

                if (provider != null)
                {
                    var orderedPaymentProvider = new OrderedPaymentProvider(provider, orderInfo.StoreInfo);

                    orderInfo.PaymentProvider = orderedPaymentProvider;

                    await UpdateOrderAndOrderInfoAsync(orderInfo)
                        .ConfigureAwait(false);
                }
            }

            return orderInfo;
        }

        public async Task<List<OrderInfo>> GetCompleteCustomerOrdersAsync(int customerId)
        {
            var orders = await _orderRepository.GetStatusOrdersAsync(
                x => x.CustomerId == customerId,
                OrderStatus.ReadyForDispatch,
                OrderStatus.OfflinePayment,
                OrderStatus.Dispatched

            ).ConfigureAwait(false);

            return orders.Select(x => new OrderInfo(x)).ToList();
        }

        public async Task<List<OrderInfo>> GetStatusOrdersAsync(params OrderStatus[] orderStatuses)
        {
            return (await _orderRepository.GetStatusOrdersAsync(null, orderStatuses).ConfigureAwait(false))
                .Select(x => new OrderInfo(x))
                .ToList();
        }
        public Task<List<OrderInfo>> GetStatusOrdersByCustomerIdAsync(params OrderStatus[] orderStatuses)
        {
            if (_ekmRequest.User?.UserId == null)
            {
                return null;
            }

            return GetStatusOrdersByCustomerIdAsync(_ekmRequest.User.UserId, orderStatuses);
        }
        public async Task<List<OrderInfo>> GetStatusOrdersByCustomerIdAsync(int customerId, params OrderStatus[] orderStatuses)
        {
            var orders = await _orderRepository.GetStatusOrdersAsync(
                x => x.CustomerId == customerId, 
                orderStatuses

            ).ConfigureAwait(false);

            return orders.Select(x => new OrderInfo(x)).ToList();
        }
        public async Task<List<OrderInfo>> GetStatusOrdersByCustomerUsernameAsync(string customerUsername, params OrderStatus[] orderStatuses)
        {
            var orders = await _orderRepository.GetStatusOrdersAsync(
                x => x.CustomerUsername == customerUsername, 
                orderStatuses
                
            ).ConfigureAwait(false);

            return orders.Select(x => new OrderInfo(x)).ToList();
        }

        public bool CheckStockAvailability(IOrderInfo orderInfo)
        {

            foreach (var line in orderInfo.OrderLines)
            {

                if (!line.Product.Backorder)
                {
                    if (line.Product.VariantGroups.Any())
                    {
                        foreach (var variant in line.Product.VariantGroups.SelectMany(x => x.Variants))
                        {
                            var variantStock = Stock.Instance.GetStock(variant.Key);

                            if (variantStock < line.Quantity)
                            {
                                return false;
                            }
                        }

                    }
                    else
                    {
                        var productStock = Stock.Instance.GetStock(line.ProductKey);

                        if (productStock < line.Quantity)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Verifies all providers match their constraints.
        /// Removes non-compliant providers
        /// 
        /// Gets called on OrderInfo updates, constraints may become invalid if the order total changes.
        /// </summary>
        private void VerifyProviders(OrderInfo orderInfo)
        {

            if (orderInfo.PaymentProvider != null ||orderInfo.ShippingProvider != null)
            {
                var total = orderInfo.OrderLineTotal.Value;
                var countryCode = orderInfo.CustomerInformation.Customer.Country;

                // Verify paymentProvider constraints
                if (orderInfo.PaymentProvider != null)
                {
                    var paymentProvider = Providers.Instance.GetPaymentProvider(orderInfo.PaymentProvider.Key);

                    if (!paymentProvider.Constraints.IsValid(countryCode, total))
                    {
                        orderInfo.PaymentProvider = null;
                    }
                }

                // Verify shipping provider constraints
                if (orderInfo.ShippingProvider != null)
                {
                    var shippingProvider = Providers.Instance.GetShippingProvider(orderInfo.ShippingProvider.Key);

                    if (!shippingProvider.Constraints.IsValid(countryCode, total))
                    {
                        orderInfo.ShippingProvider = null;
                    }
                }
            }
        }



        public Guid GetOrderIdFromCookie(string key)
        {
            var cookie = _httpCtx.Request.Cookies[key];
            if (cookie != null)
            {
                return new Guid(cookie.Value);
            }

            return Guid.Empty;
        }

        public Guid CreateOrderIdCookie(string key)
        {
            var _guid = Guid.NewGuid();
            var guidCookie = new HttpCookie(key)
            {
                Value = _guid.ToString(),
                Expires = DateTime.Now.AddDays(Configuration.Current.BasketCookieLifetime)
            };

            _httpCtx.Response.Cookies.Add(guidCookie);
            return _guid;
        }

        private void DeleteOrderCookie(string key)
        {
            var cookie = _httpCtx.Request.Cookies[key];

            if (cookie != null)
            {
                cookie.Expires = DateTime.Now.AddDays(-1);
                cookie.Value = null;

                _httpCtx.Response.SetCookie(cookie);
            }
            else
            {
                _logger.Warn<OrderService>("Could not delete order cookie. Cookie not found. Key: {Key}", key);
            }
        }

        private string GenerateOrderNumberTemplate(int referenceId)
        {
            var _referenceId = referenceId.ToString();

            if (string.IsNullOrEmpty(_store.OrderNumberTemplate))
            {
                return string.Format("{0}{1}", _store.OrderNumberPrefix, referenceId.ToString("0000"));
            }

            var template = _store.OrderNumberTemplate;

            return template.Replace("#orderId#", _referenceId).Replace("#orderIdPadded#", referenceId.ToString("0000")).Replace("#storeAlias#", _store.Alias).Replace("#day#", _date.Day.ToString()).Replace("#month#", _date.Month.ToString()).Replace("#year#", _date.Year.ToString());
        }

        private string CreateKey(string storeAlias)
        {
            var key = "ekmOrder";

            if (!Configuration.Current.ShareBasketBetweenStores)
            {
                storeAlias = string.IsNullOrEmpty(storeAlias) ? _store.Alias : storeAlias;
                key += "-" + storeAlias;
            }

            return key;
        }
    }
}
