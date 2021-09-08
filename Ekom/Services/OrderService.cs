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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Services;

namespace Ekom.Services
{
    /// <summary>
    /// GetOrder and Caching <br />
    /// <br />
    /// When an OrderInfo is created, it is persisted in Sql immediately. 
    /// As a part of that creation, modifications are made which are in turn persisted in sql. <br />
    /// A cookie with UniqueId is returned in Response. <br />
    /// When the next request arrives, 
    /// it uses GetOrder falling back to retrieving from Sql if obj is not in cache.
    /// It then makes changes, modifying the OrderInfo object now referenced by the runtime cache 
    /// and finally persisting to Sql. <br />
    /// When the third request arrives making more modifications, 
    /// GetOrder will return the modified OrderInfo from cache, 
    /// or from Sql if a restart happened.<br />
    /// If an event handler fires, either following creation of an OrderInfo or for subsequent requests,
    /// that event handler will follow the same rules as the above cases.
    /// It only diverges in regards to where it reads the cookie from, 
    /// for event handlers they will likely find the UniqueId in the response cookie.
    /// <br />
    /// In all cases these requests will have the most up to date OrderInfo. <br />
    /// <br />
    /// Locking <br />
    /// <br />
    /// A possible alternative to the current code would be to lock always after grabbing the OrderInfo object<br />
    /// Now although two methods might both complete grabbing OrderInfo at the same time and only one continues,
    /// there should be no issue since they should be holding a reference to the same object 
    /// (so the latter one isn't missing any data).<br />
    /// We could then look for a reference to the SemaphoreSlim inside HttpContext.Items in HttpHandlers 
    /// before returning the request to take care of Release()'ing the lock.<br />
    /// Problems with this approach are that many of the methods contained herein are riddled with calls to grab OrderInfo,
    /// likely some paths will grab it multiple times over the course of a call to the service.
    /// The question then becomes, where would we place the lock..<br />
    /// <br />
    /// A better solution might be to attempt a lock in an http handler
    /// and release again at the end of the module pipeline.
    /// At present I'm not confident enough regarding HttpModule specifics to attempt this. 
    /// came across SO posts regarding events firing twice 
    /// and docs regarding re-use of HttpApplication 
    /// and other stuff which put me off the whole thing..
    /// </summary>
    partial class OrderService
    {
        readonly ILogger _logger;
        readonly HttpContextBase _httpCtx;
        readonly IAppPolicyCache _runtimeCache;
        readonly IMemberService _memberService;
        readonly DiscountCache _discountCache;
        readonly IActivityLogRepository _activityLogRepository;
        readonly IOrderRepository _orderRepository;
        readonly ICouponRepository _couponRepository;
        readonly IStoreService _storeSvc;
        readonly ContentRequest _ekmRequest;

        /// <summary>
        /// Ensure all future usages of date for this request point to the same time
        /// </summary>
        readonly DateTime _date;

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
            _runtimeCache = appCaches.RuntimeCache;
            _memberService = memberService;

            _date = DateTime.Now;
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
            _ekmRequest = appCaches.RequestCache.GetCacheItem<ContentRequest>("ekmRequest");
        }

        public Task<OrderInfo> GetOrderAsync(string storeAlias)
        {
            var store = _storeSvc.GetStoreByAlias(storeAlias);

            return GetOrderAsync(store);
        }

        public async Task<OrderInfo> GetOrderAsync(IStore store)
        {
            if (Configuration.Current.UserBasket)
            {
                if (_ekmRequest.User != null && !string.IsNullOrEmpty(_ekmRequest.User.Username))
                {
                    var orderInfo = GetOrder(_ekmRequest.User.OrderId);

                    return await ReturnNonFinalOrderAsync(orderInfo).ConfigureAwait(false);
                }
            }
            else
            {
                var key = CreateKey(store.Alias);
                // Get Cart UniqueId from Cookie.
                var orderUniqueId = GetOrderIdFromCookie(key);

                // If Cookie Exist then return Cart
                if (orderUniqueId != Guid.Empty)
                {
                    var orderInfo = GetOrder(orderUniqueId);

                    _logger.Debug<OrderService>("GetOrderAsync - Found order with {UniqueId}", orderInfo?.UniqueId);

                    //// If the cart is not in the session, fetch order from sql and insert to session
                    //if (ApplicationContext.Current.ApplicationCache.RuntimeCache.GetCacheItem(key) == null)
                    //{
                    //    _log.Debug("Order is not in the session. Creating from sql");

                    //    var order = GetOrder(orderUniqueId);

                    //    ApplicationContext.Current.ApplicationCache.RuntimeCache.InsertCacheItem<OrderInfo>(key,) = order;
                    //}

                    //var orderInfo = (OrderInfo)_httpCtx.Session[key];

                    return await ReturnNonFinalOrderAsync(orderInfo).ConfigureAwait(false);
                }
            }

            return null;
        }

        /// <summary>
        /// Don't return a final order, they are closed for modification and should be viewed as receipts.
        /// Final orders are retrieved differently.
        /// 
        /// Orders while waiting for payment
        ///	we must stop modification of orders during and after payment
        ///		Example: User sent to valitor to pay, completes payment, valitor takes an hour to send callback, meanwhile user fiddles with his cart while twiddling his thumb and everything goes to shit non-maliciously
        ///			this could happen with simple amount validation as well
        ///	simple validation, compare amount paid and stored order payment amount
        ///		not good if user changes cart but keeps amount to some unknown gain
        ///	more complex validation, compare orderinfo objects or hash orderinfo
        ///		this is more complicated than it sounds, you would need to compare/hash orderlines, payment providers, discounts, coupons, shipping providers and more but not status, not dates........
        ///			what if we change something, add something, we must make sure to modify validation, this will break, will suck, cities will burn and people will die
        ///	
        ///	from my point of view it seems natural to lock the order while we wait for payment.
        ///		What if the user wants to check payment terms for different loan providers, pressing back button after each one
        ///			create new order from old one
        ///
        /// </summary>
        /// <param name="orderInfo"></param>
        /// <returns></returns>
        private async Task<OrderInfo> ReturnNonFinalOrderAsync(OrderInfo orderInfo)
        {
            if (orderInfo?.OrderStatus == OrderStatus.WaitingForPayment)
            {
                _logger.Debug<OrderService>(
                    "ReturnNonFinalOrderAsync {UniqueId} - OrderStatus == WaitingForPayment - Cloning old orderdata to new",
                    orderInfo.UniqueId
                );

                var newOrder = await CreateEmptyOrderAsync(orderInfo.StoreInfo.Alias)
                    .ConfigureAwait(false);

                // Prefer this to the other way around since new data added is
                // less likely to pertain to uniqueness.
                var oldData = orderInfo.OrderDataClone();
                oldData.UniqueId = newOrder.UniqueId;
                oldData.ReferenceId = newOrder.ReferenceId;
                oldData.OrderStatus = newOrder.OrderStatus;
                oldData.OrderNumber = newOrder.OrderNumber;
                oldData.CreateDate = newOrder.CreateDate;
                oldData.UpdateDate = newOrder.UpdateDate;

                newOrder = new OrderInfo(oldData);

                // Fixes the remaining outdated data
                await UpdateOrderAndOrderInfoAsync(newOrder, false)
                    .ConfigureAwait(false);
                return newOrder;
            }

            if (!Order.IsOrderFinal(orderInfo?.OrderStatus))
            {
                return orderInfo;
            }

            _logger.Debug<OrderService>(
                "ReturnNonFinalOrderAsync {UniqueId} - Requested order is final or not found",
                orderInfo.UniqueId);

            return null;
        }

        public Task<OrderInfo> GetCompletedOrderAsync(string storeAlias)
        {
            // Add timelimit to get the order ? Maybe 1-2 hours ?

            if (Configuration.Current.UserBasket)
            {
                if (_ekmRequest.User != null && !string.IsNullOrEmpty(_ekmRequest.User.Username))
                {
                    var orderInfo = GetOrder(_ekmRequest.User.OrderId);

                    if (Order.IsOrderFinal(orderInfo?.OrderStatus))
                    {
                        return Task.FromResult(orderInfo);
                    }
                }
            }
            else
            {
                var key = CreateKey(storeAlias);
                // Get Cart UniqueId from Cookie.
                var orderUniqueId = GetOrderIdFromCookie(key);

                // If Cookie Exist then return Cart
                if (orderUniqueId != Guid.Empty)
                {
                    var orderInfo = GetOrder(orderUniqueId);

                    if (Order.IsOrderFinal(orderInfo?.OrderStatus))
                    {
                        return Task.FromResult(orderInfo);
                    }
                }
            }

            return Task.FromResult<OrderInfo>(null);
        }

        public OrderInfo GetOrder(Guid uniqueId)
        {
            // Check for cache ?
            return _runtimeCache.GetCacheItem(
                uniqueId.ToString(),
                () => GetOrderInfoAsync(uniqueId).Result,
                Configuration.orderInfoCacheTime);
        }

        private async Task<OrderInfo> GetOrderInfoAsync(Guid uniqueId)
        {
            var orderData = await _orderRepository.GetOrderAsync(uniqueId)
                .ConfigureAwait(false);

            // Here we check if there is any OrderInfo at all to create an OrderInfo out of
            // What could happen was that during the first AddOrderLine call, an empty order is created.
            // Following that an exception is hit during new OrderLine f.x. when calling overridden getters on Products (calling Nav f.x. ?)
            // This would leave a misshapen OrderData record in sql with OrderInfo null.
            // for some reason the OrderInfo constructor allows creation despite orderData.OrderInfo == null
            // so we break here instead (??)
            return orderData?.OrderInfo != null ? new OrderInfo(orderData) : null;
        }

        public async Task ChangeOrderStatusAsync(
            Guid uniqueId,
            OrderStatus status,
            string userName = null,
            ChangeOrderSettings settings = null)
        {
            // ToDo: Lock

            if (settings == null)
            {
                settings = new ChangeOrderSettings();
            }

            var order = await _orderRepository.GetOrderAsync(uniqueId)
                    .ConfigureAwait(false);

            if (order == null)
            {
                throw new OrderInfoNotFoundException();
            }

            var oldStatus = order.OrderStatus;

            if (settings.FireOnOrderStatusChangingEvent)
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
            if (Order.IsOrderFinal(order.OrderStatus))
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

            _runtimeCache.InsertCacheItem<OrderInfo>(
                uniqueId.ToString(),
                () => new OrderInfo(order),
                Configuration.orderInfoCacheTime);

            if (settings.FireOnOrderStatusChangingEvent)
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

        public async Task<OrderInfo> UpdateOrderlineQuantityAsync(
            Guid orderLineId,
            int quantity,
            string storeAlias,
            OrderSettings settings = null
        )
        {
            var store = _storeSvc.GetStoreByAlias(storeAlias);

            if (settings == null)
            {
                settings = new OrderSettings();
            }

            OrderInfo orderInfo;
            if (settings.OrderInfo == null)
            {
                orderInfo = await GetOrderAsync(store).ConfigureAwait(false);
            }
            else
            {
                orderInfo = settings.OrderInfo as OrderInfo;
            }

            if (orderInfo == null)
            {
                throw new OrderInfoNotFoundException();
            }

            var semaphore = GetOrderLock(orderInfo);
            if (!settings.IsEventHandler)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                var orderline = orderInfo.orderLines.FirstOrDefault(x => x.Key == orderLineId);

                if (orderline == null)
                {
                    throw new OrderLineNotFoundException("Could not find order line with key: " + orderLineId);
                }

                int existingStock;
                var product = Catalog.Instance.GetProduct(storeAlias, orderline.ProductKey);
                IVariant variant = null;
                if (orderline.Product.VariantGroups.Any(g => g.Variants.Any())) 
                {
                    var orderedVariant = orderline.Product.VariantGroups.First().Variants.First();
                    variant = Catalog.Instance.GetVariant(orderedVariant.Key);
                    existingStock = variant.Stock;
                }
                else
                {
                    existingStock = product.Stock;
                }

                VerifyStock(quantity, existingStock, product, variant);

                orderline.Quantity = quantity;

                return await UpdateOrderAndOrderInfoAsync(orderInfo)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (!settings.IsEventHandler)
                {
                    semaphore.Release();
                }
            }
        }

        public async Task ChangeCurrencyAsync(Guid uniqueId, string currency, string storeAlias)
        {
            var store = _storeSvc.GetStoreByAlias(storeAlias);

            var storeCurrency = store.Currencies.FirstOrDefault(x => x.CurrencyValue == currency);

            if (storeCurrency != null)
            {
                // ToDo: Lock
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

                    _runtimeCache.InsertCacheItem<OrderInfo>(
                        uniqueId.ToString(),
                        () => new OrderInfo(order),
                        Configuration.orderInfoCacheTime);

                    _logger.Debug<OrderService>(
                        "Change Currency {OldCurrency}  to {Currency}",
                        oldCurrency,
                        currency);
                }
            }
        }

        public async Task UpdatePaidDateAsync(Guid uniqueId)
        {
            // ToDo: Lock
            var order = await _orderRepository.GetOrderAsync(uniqueId)
                .ConfigureAwait(false);

            order.PaidDate = DateTime.Now;

            await _orderRepository.UpdateOrderAsync(order)
                .ConfigureAwait(false);

            _runtimeCache.Clear(uniqueId.ToString());

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

            if (product == null)
            {
                throw new ProductNotFoundException("Unable to find product with key " + productKey);
            }

            IVariant variant = null;
            if (settings?.VariantKey != null)
            {
                variant = Catalog.Instance.GetVariant(settings.VariantKey.Value);

                if (variant == null)
                {
                    throw new VariantNotFoundException("Unable to find variant with key " + settings.VariantKey);
                }
            }

            var store = _storeSvc.GetStoreByAlias(storeAlias);

            return await AddOrderLineAsync(
                product,
                quantity,
                store,
                settings?.OrderAction,
                variant,
                settings
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
            OrderSettings settings = null
        )
        {
            if (settings == null)
            {
                settings = new OrderSettings();
            }

            // If cart action is null then AddOrUpdate is the default state
            var cartAction = action != null ? action.Value : OrderAction.AddOrUpdate;

            OrderInfo orderInfo;
            if (settings.OrderInfo == null)
            {
                orderInfo = await GetOrderAsync(store).ConfigureAwait(false);
            }
            else
            {
                orderInfo = settings.OrderInfo as OrderInfo;
            }

            if (orderInfo == null)
            {
                orderInfo = await CreateEmptyOrderAsync(store.Alias).ConfigureAwait(false);
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
                store.Alias,
                cartAction
            );

            await AddOrderLineToOrderInfoAsync(
                orderInfo,
                product,
                quantity,
                cartAction,
                variant,
                settings).ConfigureAwait(false);

            return orderInfo;
        }

        public async Task<OrderInfo> RemoveOrderLineProductAsync(
            Guid productKey,
            string storeAlias,
            RemoveOrderSettings settings = null)
        {
            OrderInfo orderInfo;
            if (settings.OrderInfo == null)
            {
                orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);
            }
            else
            {
                orderInfo = settings.OrderInfo as OrderInfo;
            }

            if (orderInfo == null)
            {
                throw new OrderInfoNotFoundException();
            }

            OrderLine existingOrderLine = null;

            var semaphore = GetOrderLock(orderInfo);
            if (!settings.IsEventHandler)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                if (orderInfo.OrderLines != null)
                {
                    if (settings?.VariantKey != null)
                    {
                        existingOrderLine
                            = orderInfo.OrderLines
                                .FirstOrDefault(
                                    x => x.Product.Key == productKey
                                    && x.Product.VariantGroups
                                        .Any(b => b.Variants.Any(z => z.Key == settings?.VariantKey)))
                                as OrderLine;
                    }
                    else
                    {
                        existingOrderLine
                            = orderInfo.OrderLines.FirstOrDefault(x => x.Product.Key == productKey)
                            as OrderLine;
                    }
                }
            }
            finally
            {
                if (!settings.IsEventHandler)
                {
                    semaphore.Release();
                }
            }

            if (existingOrderLine == null)
            {
                throw new OrderLineNotFoundException("Could not find order line with the given product or variant");
            }

            return await RemoveOrderLineAsync(existingOrderLine.Key, storeAlias, settings)
                .ConfigureAwait(false);
        }

        public async Task<OrderInfo> RemoveOrderLineAsync(
            Guid lineId,
            string storeAlias,
            OrderSettings settings = null)
        {
            _logger.Debug<OrderService>("Remove OrderLine... LineId: " + lineId);

            if (settings == null)
            {
                settings = new OrderSettings();
            }
            OrderInfo orderInfo;
            if (settings.OrderInfo == null)
            {
                orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);
            }
            else
            {
                orderInfo = settings.OrderInfo as OrderInfo;
            }

            if (orderInfo == null)
            {
                throw new OrderInfoNotFoundException();
            }

            var semaphore = GetOrderLock(orderInfo);
            if (!settings.IsEventHandler)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                var orderLine = orderInfo.OrderLines.FirstOrDefault(x => x.Key == lineId);

                if (orderLine != null)
                {
                    RemoveOrderLine(orderInfo, orderLine as OrderLine);
                }
                else
                {
                    throw new OrderLineNotFoundException("Could not find order line with key: " + lineId);
                }

                return await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (!settings.IsEventHandler)
                {
                    semaphore.Release();
                }
            }
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
        private async Task<OrderInfo> AddOrderLineToOrderInfoAsync(
            OrderInfo orderInfo,
            IProduct product,
            int quantity,
            OrderAction action,
            IVariant variant,
            OrderSettings settings
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

            var semaphore = GetOrderLock(orderInfo);
            if (!settings.IsEventHandler)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                var lineId = Guid.NewGuid();

                _logger.Debug<OrderService>(
                    "Order: {OrderNumber} Product Key: {ProductKey} Variant: {VariantKey} Action: {Action}",
                    orderInfo.OrderNumber,
                    product.Key,
                    variant?.Key,
                    action);

                OrderLine orderLine = null;
                int existingStock;

                if (variant != null)
                {
                    existingStock = variant.Stock;
                    orderLine
                        = orderInfo.OrderLines
                            .FirstOrDefault(
                                x => x.Product.Key == product.Key
                                && x.Product.VariantGroups
                                    .Any(b => b.Variants.Any(z => z.Key == variant.Key)))
                            as OrderLine;
                }
                else
                {
                    existingStock = product.Stock;
                    orderLine
                        = orderInfo.OrderLines.FirstOrDefault(x => x.Product.Key == product.Key)
                        as OrderLine;
                }

                if (orderLine != null)
                {
                    _logger.Debug<OrderService>("AddOrderLineToOrderInfo: existingOrderLine Found");

                    // Update orderline quantity with value
                    if (action == OrderAction.Set)
                    {
                        VerifyStock(quantity, existingStock, product, variant);
                        orderLine.Quantity = quantity;
                    }
                    else
                    {
                        if (orderLine.Quantity + quantity < 0)
                        {
                            throw new OrderLineNegativeException("OrderLines cannot be updated to negative quantity");
                        }

                        VerifyStock(quantity + orderLine.Quantity, existingStock, product, variant);

                        orderLine.Quantity += quantity;

                        // If the update action ends up setting quantity to zero we remove the order line
                        if (orderLine.Quantity == 0)
                        {
                            RemoveOrderLine(orderInfo, orderLine);
                        }
                    }
                }
                else
                {
                    if (quantity < 0)
                    {
                        throw new OrderLineNegativeException("OrderLines cannot be created with negative quantity");
                    }

                    VerifyStock(quantity, existingStock, product, variant);

                    // Update orderline when adding product to orderline

                    _logger.Debug<OrderService>("AddOrderLineToOrderInfo: existingOrderLine Not Found");

                    orderLine = new OrderLine(
                        product,
                        quantity,
                        lineId,
                        orderInfo,
                        variant
                    );

                    orderInfo.orderLines.Add(orderLine);

                    // Product discounts do not contain constraints that change with quantity updates or order modifications
                    // It's therefore enough to only check on OrderLine creation
                    if (product.ProductDiscount() != null
                    // Make sure that the current OrderInfo discount, if there is one, is inclusive
                    // Meaning you can apply this discount while having a separate discount 
                    // affecting other OrderLines
                    && (orderInfo.Discount == null || orderInfo.Discount.Stackable))
                    {
                        _logger.Debug<OrderService>(
                            "Discount {ProductDiscountKey} found on product, applying to OrderLine",
                            product.ProductDiscount().Key);
                        await ApplyDiscountToOrderLineAsync(
                            orderLine,
                            product.ProductDiscount(),
                            orderInfo,
                            new DiscountOrderSettings
                            {
                                UpdateOrder = false,
                            }
                        ).ConfigureAwait(false);
                    }

                }

                return await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (!settings.IsEventHandler)
                {
                    semaphore.Release();
                }
            }
        }

        private async Task<OrderInfo> UpdateOrderAndOrderInfoAsync(
            OrderInfo orderInfo,
            bool fireOnOrderUpdatedEvents = true)
        {
            _logger.Debug<OrderService>("Update Order with new OrderInfo");

            VerifyProviders(orderInfo);
            VerifyDiscounts(orderInfo);
            AddGlobalDiscounts(orderInfo);

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

                if (culture.TwoLetterISOLanguageName == "is")
                {
                    culture = Configuration.IsCultureInfo;
                }

                orderData.Currency = culture.NumberFormat.CurrencySymbol;
            }
            catch (ArgumentException)
            {
                orderData.Currency = orderInfo.StoreInfo.Culture;
            }

            await _orderRepository.UpdateOrderAsync(orderData)
                .ConfigureAwait(false);
            UpdateOrderInfoInCache(orderInfo);

            if (fireOnOrderUpdatedEvents)
            {
                Order.OnOrderUpdated(this, new OrderUpdatedEventArgs
                {
                    OrderInfo = orderInfo,
                });
            }

            // Regardless of modifications from event handlers,
            // everybody references the same OrderInfo object
            return orderInfo;
        }

        /// <summary>
        /// Is this necessary?
        /// Likely not, but is cheap.
        /// 
        /// See above for notes on GetOrder and caching
        /// </summary>
        /// <param name="orderInfo"></param>
        private void UpdateOrderInfoInCache(OrderInfo orderInfo)
        {
            var key = CreateKey(orderInfo.StoreInfo.Alias);

            _runtimeCache.InsertCacheItem<OrderInfo>(
                orderInfo.UniqueId.ToString(),
                () => orderInfo,
                Configuration.orderInfoCacheTime);
        }

        public async Task AddHangfireJobsToOrderAsync(string storeAlias, IEnumerable<string> hangfireJobs)
        {
            var orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);
            if (orderInfo == null)
            {
                throw new OrderInfoNotFoundException();
            }

            var semaphore = GetOrderLock(orderInfo);
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                orderInfo._hangfireJobs.AddRange(hangfireJobs);

                await UpdateOrderAndOrderInfoAsync(orderInfo)
                    .ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task RemoveHangfireJobsToOrderAsync(string storeAlias)
        {
            var orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);
            if (orderInfo == null)
            {
                throw new OrderInfoNotFoundException();
            }

            var semaphore = GetOrderLock(orderInfo);
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                orderInfo._hangfireJobs.Clear();

                await UpdateOrderAndOrderInfoAsync(orderInfo)
                    .ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<OrderInfo> CreateEmptyOrderAsync(string storeAlias)
        {
            _logger.Debug<OrderService>("CreateEmptyOrderAsync..");

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

            var store = _storeSvc.GetStoreByAlias(storeAlias);

            var orderdata = await SaveEmptyOrderDataAsync(orderUniqueId, store)
                .ConfigureAwait(false);

            return new OrderInfo(orderdata, store);
        }
        private async Task<OrderData> SaveEmptyOrderDataAsync(Guid uniqueId, IStore store)
        {
            _logger.Debug<OrderService>("SaveEmptyOrderDataAsync Store: {Store}", store.Alias);

            var orderData = new OrderData
            {
                UniqueId = uniqueId,
                CreateDate = _date,
                StoreAlias = store.Alias,
                OrderStatus = OrderStatus.Incomplete,
                Currency = store.Currency.ISOCurrencySymbol,
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

            orderData.OrderNumber = GenerateOrderNumberTemplate(orderData.ReferenceId, store);
            await _orderRepository.UpdateOrderAsync(orderData)
                .ConfigureAwait(false);

            return orderData;
        }

        public async Task<OrderInfo> UpdateCustomerInformationAsync(
            Dictionary<string, string> form,
            OrderSettings settings = null)
        {
            _logger.Debug<OrderService>("UpdateCustomerInformation...");

            if (settings == null)
            {
                settings = new OrderSettings();
            }


            if (form.ContainsKey("storeAlias"))
            {
                var storeAlias = form["storeAlias"];

                OrderInfo orderInfo;
                if (settings.OrderInfo == null)
                {
                    orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);
                }
                else
                {
                    orderInfo = settings.OrderInfo as OrderInfo;
                }

                if (form.ContainsKey("ShippingProvider"))
                {
                    if (Guid.TryParse(form["ShippingProvider"], out Guid _providerKey))
                    {
                        orderInfo = await UpdateShippingInformationAsync(_providerKey, storeAlias, settings).ConfigureAwait(false);
                    }
                }

                if (form.ContainsKey("PaymentProvider"))
                {
                    if (Guid.TryParse(form["PaymentProvider"], out Guid _providerKey))
                    {
                        orderInfo = await UpdatePaymentInformationAsync(_providerKey, storeAlias, settings).ConfigureAwait(false);
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

                return await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent)
                    .ConfigureAwait(false);
            }

            throw new ArgumentException("storeAlias parameter missing from form", nameof(form));
        }

        public async Task<OrderInfo> UpdateShippingInformationAsync(
            Guid shippingProviderId,
            string storeAlias,
            OrderSettings settings = null)
        {
            _logger.Debug<OrderService>("UpdateShippingInformation...");

            var store = _storeSvc.GetStoreByAlias(storeAlias);

            if (settings == null)
            {
                settings = new OrderSettings();
            }
            OrderInfo orderInfo;
            if (settings.OrderInfo == null)
            {
                orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);
            }
            else
            {
                orderInfo = settings.OrderInfo as OrderInfo;
            }
            if (orderInfo == null)
            {
                throw new OrderInfoNotFoundException();
            }

            var semaphore = GetOrderLock(orderInfo);
            if (!settings.IsEventHandler)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                if (shippingProviderId != Guid.Empty)
                {
                    var provider = API.Providers.Instance.GetShippingProvider(shippingProviderId, store);

                    if (provider != null)
                    {

                        var orderedShippingProvider = new OrderedShippingProvider(provider, orderInfo.StoreInfo);

                        orderInfo.ShippingProvider = orderedShippingProvider;

                        return await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent)
                            .ConfigureAwait(false);
                    }
                }

                return orderInfo;
            }
            finally
            {
                if (!settings.IsEventHandler)
                {
                    semaphore.Release();
                }
            }
        }

        public async Task<OrderInfo> UpdatePaymentInformationAsync(
            Guid paymentProviderId,
            string storeAlias,
            OrderSettings settings = null)
        {
            _logger.Debug<OrderService>("UpdatePaymentInformation...");

            var store = _storeSvc.GetStoreByAlias(storeAlias);

            if (settings == null)
            {
                settings = new OrderSettings();
            }
            OrderInfo orderInfo;
            if (settings.OrderInfo == null)
            {
                orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);
            }
            else
            {
                orderInfo = settings.OrderInfo as OrderInfo;
            }
            if (orderInfo == null)
            {
                throw new OrderInfoNotFoundException();
            }

            var semaphore = GetOrderLock(orderInfo);
            if (!settings.IsEventHandler)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                if (paymentProviderId != Guid.Empty)
                {
                    var provider = API.Providers.Instance.GetPaymentProvider(paymentProviderId, store);

                    if (provider != null)
                    {
                        var orderedPaymentProvider = new OrderedPaymentProvider(provider, orderInfo.StoreInfo);

                        orderInfo.PaymentProvider = orderedPaymentProvider;

                        return await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent)
                            .ConfigureAwait(false);
                    }
                }

                return orderInfo;
            }
            finally
            {
                if (!settings.IsEventHandler)
                {
                    semaphore.Release();
                }
            }
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
                return Task.FromResult<List<OrderInfo>>(null);
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

        [Obsolete("This assumes the OrderInfo has been modified already, " +
            "not useful for order modifications since on errors you would have to roll back")]
        private bool CheckStockAvailability(IOrderInfo orderInfo)
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

        private void VerifyStock(int quantity, int existingStock, IProduct product, IVariant variant = null)
        {
            if (!Configuration.Current.DisableStock
            && !product.Backorder
            && existingStock < quantity)
            {
                throw new NotEnoughStockException(
                    $"Stock not available for product {product.Key} and variant {variant?.Key}");
            }
        }

        /// <summary>
        /// Verifies all providers match their constraints.
        /// Removes non-compliant providers
        /// 
        /// Gets called on OrderInfo updates, constraints may become invalid if the order total changes.
        /// </summary>
        private void VerifyProviders(OrderInfo orderInfo)
        {
            if (orderInfo.PaymentProvider != null || orderInfo.ShippingProvider != null)
            {
                var total = orderInfo.GrandTotal.Value;
                var countryCode = orderInfo.CustomerInformation.Customer.Country;

                // Verify paymentProvider constraints
                if (orderInfo.PaymentProvider != null)
                {
                    var paymentProvider = Providers.Instance.GetPaymentProvider(orderInfo.PaymentProvider.Key);

                    // In case of deletion
                    if (paymentProvider == null
                    || !paymentProvider.Constraints.IsValid(countryCode, total))
                    {
                        orderInfo.PaymentProvider = null;
                    }
                }

                // Verify shipping provider constraints
                if (orderInfo.ShippingProvider != null)
                {
                    var shippingProvider = Providers.Instance.GetShippingProvider(orderInfo.ShippingProvider.Key);

                    if (shippingProvider == null
                    || !shippingProvider.Constraints.IsValid(countryCode, total))
                    {
                        orderInfo.ShippingProvider = null;
                    }
                }
            }
        }

        private Guid GetOrderIdFromCookie(string key)
        {
            HttpCookie cookie = null;

            // Applicable when the order was created in this request
            // This enables support for event handlers accessing the api and modifying order info
            // during the request that created the OrderInfo
            if (_httpCtx.Response.Cookies.AllKeys.Contains(key))
            // The response cookie collection has extremely specific behavior
            // Any key accessed will by default create and return a fresh cookie, 
            // regardless of it existing or not beforehand. (previous cookies are overwritten this way)
            // Therefore we check AllKeys before accessing the collection directly
            {
                cookie = _httpCtx.Response.Cookies[key];
            }

            if (cookie == null)
            {
                cookie = _httpCtx.Request.Cookies[key];
            }

            if (cookie != null)
            {
                return new Guid(cookie.Value);
            }

            return Guid.Empty;
        }

        private Guid CreateOrderIdCookie(string key)
        {
            var guid = Guid.NewGuid();
            var guidCookie = new HttpCookie(key)
            {
                Value = guid.ToString(),
                Expires = DateTime.Now.AddDays(Configuration.Current.BasketCookieLifetime)
            };

            _httpCtx.Response.Cookies.Add(guidCookie);
            return guid;
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

        private string GenerateOrderNumberTemplate(int referenceId, IStore store)
        {
            var _referenceId = referenceId.ToString();

            if (string.IsNullOrEmpty(store.OrderNumberTemplate))
            {
                return string.Format("{0}{1}", store.OrderNumberPrefix, referenceId.ToString("0000"));
            }

            var template = store.OrderNumberTemplate;

            return template
                .Replace("#orderId#", _referenceId)
                .Replace("#orderIdPadded#", referenceId.ToString("0000"))
                .Replace("#storeAlias#", store.Alias)
                .Replace("#day#", _date.Day.ToString())
                .Replace("#month#", _date.Month.ToString())
                .Replace("#year#", _date.Year.ToString());
        }

        private string CreateKey(string storeAlias)
        {
            var key = "ekmOrder";

            if (!Configuration.Current.ShareBasketBetweenStores)
            {
                key += "-" + storeAlias;
            }

            return key;
        }

        /// See comments for service and under <see cref="OrderSettings"/>
        private SemaphoreSlim GetOrderLock(IOrderInfo orderInfo)
            => _orderLocks.GetOrAdd(orderInfo.UniqueId, new SemaphoreSlim(1, 1));

        /// See comments for service and under <see cref="OrderSettings"/>
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _orderLocks
            = new ConcurrentDictionary<Guid, SemaphoreSlim>();
    }
}
