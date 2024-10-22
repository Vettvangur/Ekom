using Ekom.API;
using Ekom.Cache;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Globalization;

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
        readonly Configuration _config;
        readonly ILogger<OrderService> _logger;
        readonly HttpContext _httpCtx;
        readonly IMemoryCache _memoryCache;
        readonly IMemberService _memberService;
        readonly DiscountCache _discountCache;
        readonly ActivityLogRepository _activityLogRepository;
        readonly OrderRepository _orderRepository;
        readonly CouponRepository _couponRepository;
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
            Configuration config,
            OrderRepository orderRepo,
            CouponRepository couponRepository,
            ActivityLogRepository activityLogRepository,
            ILogger<OrderService> logger,
            IStoreService storeService,
            IMemoryCache memoryCache,
            IMemberService memberService,
            DiscountCache discountCache)
        {
            _logger = logger;

            _config = config;
            _orderRepository = orderRepo;
            _couponRepository = couponRepository;
            _activityLogRepository = activityLogRepository;
            _storeSvc = storeService;
            _discountCache = discountCache;
            _memoryCache = memoryCache;
            _memberService = memberService;

            _date = DateTime.Now;
        }

        /// <summary>
        /// ctor
        /// </summary>
        public OrderService(
            Configuration config,
            OrderRepository orderRepo,
            CouponRepository couponRepository,
            ActivityLogRepository activityLogRepository,
            ILogger<OrderService> logger,
            IStoreService storeService,
            IMemoryCache memoryCache,
            IMemberService memberService,
            DiscountCache discountCache,
            IHttpContextAccessor httpContextAccessor)
            : this(config, orderRepo, couponRepository, activityLogRepository, logger, storeService, memoryCache, memberService, discountCache)
        {

            try
            {
                _httpCtx = httpContextAccessor.HttpContext;

                var r = _httpCtx?.Items["umbrtmche-ekmRequest"] as Lazy<object>;

                if (r != null && r.Value != null)
                {
                    _ekmRequest = r.Value as ContentRequest;
                }
            }
            catch
            {
                _logger.LogWarning("HttpContext is null trying to fetch umbrtmche-ekmRequest, likely running in Hangfire or similar.");
            }

        }

        public Task<OrderInfo> GetOrderAsync(string storeAlias)
        {
            var store = _storeSvc.GetStoreByAlias(storeAlias);

            if (store == null)
            {
                throw new ArgumentNullException($"Could not find store with the alias {storeAlias}");
            }

            return GetOrderAsync(store);
        }

        public async Task<OrderInfo> GetOrderAsync(IStore store)
        {
            if (store.UserBasket && !string.IsNullOrEmpty(_ekmRequest?.User?.Username))
            {
                var orderInfo = await GetOrderAsync(_ekmRequest.User.OrderId).ConfigureAwait(false);

                return await ReturnNonFinalOrderAsync(orderInfo).ConfigureAwait(false);
            }
            else
            {
                var key = CreateKey(store);
                // Get Cart UniqueId from Cookie.
                var orderUniqueId = GetOrderIdFromCookie(key);

                // If Cookie Exist then return Cart
                if (orderUniqueId != Guid.Empty)
                {
                    var orderInfo = await GetOrderAsync(orderUniqueId).ConfigureAwait(false);

                    _logger.LogDebug("GetOrderAsync - Found order with {UniqueId}", orderInfo?.UniqueId);

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
        ///		Example: User sent to valitor to pay, completes payment, valitor takes an hour to send callback, meanwhile user fiddles with his cart while twiddling his thumb and everything goes wrong non-maliciously
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
        /// 2023-04-3 - This doesn't work great with most payment providers
        /// As they are async, receipt pages often have to show order data from WaitingForPayment orders.
        /// While the receipt itself can be controlled, oftentimes the header and other portions of page are retrieving order as well.
        /// This will cause a copy to be made despite the customer just completing payment.
        /// 
        /// We probably need some more robust support of cart editing during payment.
        /// F.x. compare cart when sent to payment provider
        /// 
        /// </summary>
        /// <param name="orderInfo"></param>
        /// <returns></returns>
        private async Task<OrderInfo> ReturnNonFinalOrderAsync(OrderInfo orderInfo)
        {
            //if (orderInfo?.OrderStatus == OrderStatus.WaitingForPayment)
            //{
            //    _logger.LogDebug(
            //        "ReturnNonFinalOrderAsync {UniqueId} - OrderStatus == WaitingForPayment - Cloning old orderdata to new",
            //        orderInfo.UniqueId
            //    );

            //    var newOrder = await CreateEmptyOrderAsync(orderInfo.StoreInfo.Alias)
            //        .ConfigureAwait(false);

            //    // Prefer this to the other way around since new data added is
            //    // less likely to pertain to uniqueness.
            //    var oldData = orderInfo.OrderDataClone();
            //    oldData.UniqueId = newOrder.UniqueId;
            //    oldData.ReferenceId = newOrder.ReferenceId;
            //    oldData.OrderStatus = newOrder.OrderStatus;
            //    oldData.OrderNumber = newOrder.OrderNumber;
            //    oldData.CreateDate = newOrder.CreateDate;
            //    oldData.UpdateDate = newOrder.UpdateDate;

            //    newOrder = new OrderInfo(oldData);

            //    // Fixes the remaining outdated data
            //    await UpdateOrderAndOrderInfoAsync(newOrder, false)
            //        .ConfigureAwait(false);
            //    return newOrder;
            //}

            if (!Order.IsOrderFinal(orderInfo?.OrderStatus))
            {
                return orderInfo;
            }

            _logger.LogDebug(
                "ReturnNonFinalOrderAsync {UniqueId} - Requested order is final or not found",
                orderInfo.UniqueId);

            return null;
        }

        public async Task<OrderInfo> GetCompletedOrderAsync(string storeAlias)
        {
           
            var store = API.Store.Instance.GetStore(storeAlias);

            // Add timelimit to get the order ? Maybe 1-2 hours ?
            if (store.UserBasket && !string.IsNullOrEmpty(_ekmRequest.User.Username))
            {
                var orderInfo = await GetOrderAsync(_ekmRequest.User.OrderId).ConfigureAwait(false);

                if (Order.IsOrderFinal(orderInfo?.OrderStatus))
                {
                    return orderInfo;
                }
            }
            else
            {
                var key = CreateKey(store);
                // Get Cart UniqueId from Cookie.
                var orderUniqueId = GetOrderIdFromCookie(key);

                // If Cookie Exist then return Cart
                if (orderUniqueId == Guid.Empty) return null;
                
                var orderInfo = await GetOrderAsync(orderUniqueId).ConfigureAwait(false);

                if (Order.IsOrderFinal(orderInfo?.OrderStatus))
                {
                    return orderInfo;
                }
            }

            return null;
        }

        public Task<OrderInfo> GetOrderAsync(Guid uniqueId)
        {
            // Check for cache ?
            return _memoryCache.GetOrCreateAsync(
                uniqueId.ToString(),
                cacheEntry =>
                {
                    cacheEntry.SetAbsoluteExpiration(Configuration.orderInfoCacheTime);
                    return GetOrderInfoAsync(uniqueId);
                });
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

            var OrderStatusEventModel = new OrderStatusEventArgs()
            {
                OrderUniqueId = uniqueId,
                PreviousStatus = oldStatus,
                Status = status,
                ClearCustomerOrderReference = true
            };

            if (settings.FireOnOrderStatusChangingEvent)
            {
                OrderEvents.OnOrderStatusChanging(this, OrderStatusEventModel);
            }

            order.OrderStatus = status;

            if (OrderStatusEventModel.ClearCustomerOrderReference)
            {
                ClearCustomerOrderReference(order);
            }

            await _orderRepository.UpdateOrderAsync(order)
                .ConfigureAwait(false);

            _memoryCache.Set<OrderInfo>(
                uniqueId.ToString(),
                new OrderInfo(order),
                Configuration.orderInfoCacheTime);

            if (settings.FireOnOrderStatusChangingEvent)
            {
                OrderEvents.OnOrderStatusChanged(this, new OrderStatusEventArgs
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

            _logger.LogDebug(
                "Change Order {OrderNumber} status to {Status}",
                order.OrderNumber,
                status);
        }

        public void ClearCustomerOrderReference(OrderData order)
        {
            if (!Order.IsOrderFinal(order.OrderStatus)) return;

            if (order is { OrderStatus: OrderStatus.ReadyForDispatch, PaidDate: null })
            {
                order.PaidDate = DateTime.Now;
            }

            var userName = !string.IsNullOrEmpty(order.CustomerUsername)
                ? order?.CustomerUsername
                : _httpCtx.User.Identity?.Name;

            var store = API.Store.Instance.GetStore(order.StoreAlias);

            if (store.UserBasket && !string.IsNullOrEmpty((userName)))
            {
                _memberService.Save(new Dictionary<string, object>() {
                    { "orderId", "" }
                }, userName);
            }
            else
            {
                _memoryCache.Remove(order?.UniqueId.ToString());
            }
        }

        public async Task<OrderInfo> UpdateOrderLineQuantityAsync(
            Guid orderLineId,
            int quantity,
            string storeAlias,
            OrderSettings settings = null
        )
        {
            if (quantity <= 0)
            {
                return await RemoveOrderLineAsync(orderLineId, storeAlias, settings).ConfigureAwait(false);
            }

            var store = _storeSvc.GetStoreByAlias(storeAlias);

            if (store == null)
            {
                throw new ArgumentNullException("Store is null. " + storeAlias);
            }

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

                var product = Catalog.Instance.GetProduct(orderline.ProductKey, storeAlias);

                IVariant variant = null;

                if (orderline.Product.VariantGroups != null && orderline.Product.VariantGroups.Any(g => g.Variants.Any()))
                {
                    var orderedVariant = orderline.Product.VariantGroups.First().Variants.First();
                    variant = Catalog.Instance.GetVariant(orderedVariant.Key, storeAlias);
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

        public async Task<IOrderInfo> ChangeCurrencyAsync(Guid uniqueId, string currency, string storeAlias)
        {
            var store = _storeSvc.GetStoreByAlias(storeAlias);

            var storeCurrency = store.Currencies.FirstOrDefault(x => x.CurrencyValue == currency);

            if (storeCurrency != null)
            {
                // ToDo: Lock
                var order = await _orderRepository.GetOrderAsync(uniqueId).ConfigureAwait(false);

                var oldCurrency = order.Currency;

                order.Currency = storeCurrency.ISOCurrencySymbol;

                var orderInfo = await GetOrderAsync(uniqueId).ConfigureAwait(false);

                if (orderInfo != null)
                {
                    orderInfo.StoreInfo.Currency = storeCurrency;

                    var serializedOrderInfo = JsonConvert.SerializeObject(orderInfo, EkomJsonDotNet.settings);

                    order.OrderInfo = serializedOrderInfo;

                    await _orderRepository.UpdateOrderAsync(order).ConfigureAwait(false);

                    orderInfo = new OrderInfo(order);

                    _memoryCache.Set<OrderInfo>(
                        uniqueId.ToString(),
                        orderInfo,
                        Configuration.orderInfoCacheTime);

                    _logger.LogDebug(
                        "Change Currency {OldCurrency}  to {Currency}",
                        oldCurrency,
                        currency);
                }

                return orderInfo;
            }

            return null;
        }

        public async Task UpdatePaidDateAsync(Guid uniqueId)
        {
            // ToDo: Lock
            var order = await _orderRepository.GetOrderAsync(uniqueId)
                .ConfigureAwait(false);

            order.PaidDate = DateTime.Now;

            await _orderRepository.UpdateOrderAsync(order)
                .ConfigureAwait(false);

            _memoryCache.Remove(uniqueId.ToString());

            _logger.LogDebug(
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

            var product = Catalog.Instance.GetProduct(productKey, storeAlias);

            if (product == null)
            {
                throw new ProductNotFoundException("Unable to find product with key " + productKey);
            }

            IVariant variant = null;
            if (settings?.VariantKey != null)
            {
                variant = Catalog.Instance.GetVariant(settings.VariantKey.Value, storeAlias);

                if (variant == null)
                {
                    throw new VariantNotFoundException("Unable to find variant with key " + settings.VariantKey);
                }

                if (variant.ProductKey != productKey)
                {
                    throw new EkomException("Mismatch between product and variant. Ensure chosen variant is a child of given Product");
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

            _logger.LogDebug("ProductId: {ProductId}" +
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

            OrderLine? existingOrderLine = null;

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
            _logger.LogDebug("Remove OrderLine... LineId: " + lineId);

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

        public async Task<OrderInfo> RemoveOrderLinesAsync(
            Guid[] lineIds,
            string storeAlias,
            OrderSettings settings = null)
        {
            _logger.LogDebug("Remove OrderLines... LineId: " + string.Join(',', lineIds));

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
                var orderLines = orderInfo.OrderLines.Where(x => lineIds.Contains(x.Key));


                if (orderLines != null && orderLines.Any())
                {
                    foreach (var orderline in orderLines)
                    {
                        RemoveOrderLine(orderInfo, orderline as OrderLine);
                    }
                   
                    return await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent)
                        .ConfigureAwait(false);
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

        private void RemoveOrderLine(OrderInfo orderInfo, OrderLine orderLine)
        {
            try
            {
                var linkedLines = orderInfo.orderLines.Where(x => x.Settings != null && x.Settings.Link == orderLine.Key).ToList();

                foreach (var linkedLine in linkedLines)
                {
                    orderInfo.orderLines.Remove(linkedLine);
                }

                orderInfo.orderLines.Remove(orderLine);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to remove orderLine");
            }
        }

        public async Task<OrderInfo> ReInitializeOrderLinesAsync(
            string storeAlias,
            OrderSettings? settings = null)
        {
            _logger.LogDebug("ReInitializeOrderLinesAsync...");

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
                var orderLines = orderInfo.OrderLines.ToList();

                if (orderLines != null && orderLines.Any())
                {
                    orderInfo.orderLines.Clear();

                    await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent)
                        .ConfigureAwait(false);
                    
                    foreach (var orderline in orderLines)
                    {
                        orderInfo = await AddOrderLineAsync(orderline.ProductKey, orderline.Quantity, storeAlias, new AddOrderSettings()
                        {
                            OrderInfo = orderInfo,
                            VariantKey = orderline.Product?.VariantGroups?.FirstOrDefault()?.Variants?.FirstOrDefault()?.Key
                        }).ConfigureAwait(false);
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

                _logger.LogDebug(
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

                if (orderLine != null && action != OrderAction.New)
                {
                    _logger.LogDebug("AddOrderLineToOrderInfo: existingOrderLine Found");

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

                    _logger.LogDebug("AddOrderLineToOrderInfo: existingOrderLine Not Found");

                    orderLine = new OrderLine(
                        product,
                        quantity,
                        lineId,
                        orderInfo,
                        variant,
                        settings.OrderDynamicRequest
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
                        _logger.LogDebug(
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
            try
            {
                _logger.LogDebug("Update Order with new OrderInfo");

                VerifyProviders(orderInfo);
                VerifyDiscounts(orderInfo);
                AddGlobalDiscounts(orderInfo);

                orderInfo.Culture = CultureInfo.CurrentCulture.Name;

                orderInfo.CustomerInformation.CustomerIpAddress = _ekmRequest?.IPAddress ?? "";

                var serializedOrderInfo = JsonConvert.SerializeObject(orderInfo, EkomJsonDotNet.settings);

                var orderData = await _orderRepository.GetOrderAsync(orderInfo.UniqueId)
                    .ConfigureAwait(false);

                if (_ekmRequest != null && _ekmRequest.User != null && !string.IsNullOrEmpty(_ekmRequest.User.Username))
                {
                    orderInfo.CustomerInformation.Customer.UserId = _ekmRequest.User.UserId;
                    orderInfo.CustomerInformation.Customer.UserName = _ekmRequest.User.Username;
                    orderData.CustomerUsername = _ekmRequest.User.Username;
                    orderData.CustomerId = _ekmRequest.User.UserId;
                }

                orderData.CustomerEmail = orderInfo.CustomerInformation.Customer.Email;
                orderData.CustomerName = orderInfo.CustomerInformation.Customer.Name;

                orderData.ShippingCountry = orderInfo.CustomerInformation.Shipping != null
                    && !string.IsNullOrEmpty(orderInfo.CustomerInformation.Shipping.Country)
                    ? orderInfo.CustomerInformation.Shipping.Country : orderInfo.CustomerInformation.Customer.Country;

                if (fireOnOrderUpdatedEvents)
                {
                    OrderEvents.OnOrderUpdateing(this, new OrderUpdatingEventArgs
                    {
                        OrderInfo = orderInfo,
                    });
                }

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

                    orderData.Currency = orderInfo.StoreInfo.Currency.ISOCurrencySymbol;
                }
                catch (ArgumentException)
                {
                    orderData.Currency = orderInfo.StoreInfo.Currency.ISOCurrencySymbol;
                }


                await _orderRepository.UpdateOrderAsync(orderData)
                    .ConfigureAwait(false);
                UpdateOrderInfoInCache(orderInfo);

                if (fireOnOrderUpdatedEvents)
                {
                    OrderEvents.OnOrderUpdated(this, new OrderUpdatedEventArgs
                    {
                        OrderInfo = orderInfo,
                    });
                }

                // Regardless of modifications from event handlers,
                // everybody references the same OrderInfo object
                return orderInfo;
            } catch(Exception ex)
            {
                _logger.LogError(ex, "UpdateOrderAndOrderInfoAsync failed: " + JsonConvert.SerializeObject(orderInfo));
                throw;
            }
           
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
            var store = API.Store.Instance.GetStore(orderInfo.StoreInfo.Alias);

            var key = CreateKey(store);

            _memoryCache.Set<OrderInfo>(
                orderInfo.UniqueId.ToString(),
                orderInfo,
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
            _logger.LogDebug("CreateEmptyOrderAsync..");

            var store = _storeSvc.GetStoreByAlias(storeAlias);

            Guid orderUniqueId;

            if (store.UserBasket && _httpCtx.User.Identity.IsAuthenticated)
            {
                orderUniqueId = Guid.NewGuid();

                _memberService.Save(new Dictionary<string, object>() {
                        { "orderId", orderUniqueId.ToString() }
                }, _httpCtx.User.Identity.Name);
            }
            else
            {
                orderUniqueId = CreateOrderIdCookie(CreateKey(store));
            }

            var orderdata = await SaveEmptyOrderDataAsync(orderUniqueId, store)
                .ConfigureAwait(false);

            return new OrderInfo(orderdata, store);
        }
        private async Task<OrderData> SaveEmptyOrderDataAsync(Guid uniqueId, IStore store)
        {
            _logger.LogDebug("SaveEmptyOrderDataAsync Store: {Store}", store.Alias);

            var orderData = new OrderData
            {
                UniqueId = uniqueId,
                CreateDate = _date,
                StoreAlias = store.Alias,
                OrderStatus = OrderStatus.Incomplete,
                Currency = store.Currency.ISOCurrencySymbol,
                UpdateDate = DateTime.Now
            };

            if(_ekmRequest?.User != null && !string.IsNullOrEmpty(_ekmRequest.User.Username))
            {
                orderData.CustomerEmail = _ekmRequest.User.Email;
                orderData.CustomerUsername = _ekmRequest.User.Username;
                orderData.CustomerId = _ekmRequest.User.UserId;
                orderData.CustomerName = _ekmRequest.User.Name?.Trim();
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
            _logger.LogDebug("UpdateCustomerInformation...");

            if (settings == null)
            {
                settings = new OrderSettings();
            }


            if (!form.TryGetValue("storeAlias", out var storeAlias))
                throw new ArgumentException("storeAlias parameter missing from form", nameof(form));
            
            OrderInfo orderInfo;
            
            if (settings.OrderInfo == null)
            {
                orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);
            }
            else
            {
                orderInfo = settings.OrderInfo as OrderInfo;
            }

            if (form.TryGetValue("ShippingProvider", out var shippingProvider))
            {
                if (Guid.TryParse(shippingProvider, out Guid _providerKey))
                {
                    var customData = form.Keys.Where(x => x != "ShippingProvider" && x.StartsWith("customshipping", StringComparison.InvariantCulture)).ToDictionary(
                        k => k,
                        v => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(form[v]));

                    orderInfo = await UpdateShippingInformationAsync(_providerKey, storeAlias, customData, settings).ConfigureAwait(false);

                }
            }

            if (form.TryGetValue("PaymentProvider", out var paymentProvider))
            {
                if (Guid.TryParse(paymentProvider, out Guid _providerKey))
                {
                    var customData = form.Keys.Where(x => x != "PaymentProvider" && x.StartsWith("custompayment", StringComparison.InvariantCulture)).ToDictionary(
                        k => k,
                        v => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(form[v]));

                    orderInfo = await UpdatePaymentInformationAsync(_providerKey, storeAlias, customData, settings).ConfigureAwait(false);
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

        public async Task<OrderInfo> UpdateShippingInformationAsync(
            Guid shippingProviderId,
            string storeAlias,
            Dictionary<string,string> customData,
            OrderSettings settings = null)
        {
            _logger.LogDebug("UpdateShippingInformation...");

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
                if (shippingProviderId == Guid.Empty) return orderInfo;

                var provider = Providers.Instance.GetShippingProvider(shippingProviderId, store);

                if (provider == null) return orderInfo;
                
                var orderedShippingProvider = new OrderedShippingProvider(provider, orderInfo.StoreInfo, customData);

                orderInfo.ShippingProvider = orderedShippingProvider;

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

        public async Task<OrderInfo> UpdatePaymentInformationAsync(
            Guid paymentProviderId,
            string storeAlias,
            Dictionary<string,string> customData,
            OrderSettings settings = null)
        {
            _logger.LogDebug("UpdatePaymentInformation...");

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
                if (paymentProviderId == Guid.Empty) return orderInfo;
                
                var provider = Providers.Instance.GetPaymentProvider(paymentProviderId, store);

                if (provider == null) return orderInfo;

                var orderedPaymentProvider = new OrderedPaymentProvider(provider, orderInfo.StoreInfo, customData);

                orderInfo.PaymentProvider = orderedPaymentProvider;

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

        public async Task<List<OrderInfo>> GetCompleteCustomerOrdersAsync(int customerId, string? storeAlias = null)
        {
            var orders = await _orderRepository.GetStatusOrdersAsync(
                x => x.CustomerId == customerId,
                OrderStatus.ReadyForDispatch,
                OrderStatus.OfflinePayment,
                OrderStatus.Dispatched

            ).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(storeAlias))
            {
                orders = orders.Where(x => x.StoreAlias == storeAlias).ToList();
            }


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
            if (!_config.DisableStock
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
            if (orderInfo.PaymentProvider == null && orderInfo.ShippingProvider == null) return;
            
            var total = orderInfo.GrandTotal.Value;
            var countryCode = orderInfo.CustomerInformation.Customer.Country;
            var shippingCountry = orderInfo.CustomerInformation.Shipping.Country ?? countryCode;

            var store = _storeSvc.GetStoreByAlias(orderInfo.StoreInfo.Alias);

            // Verify paymentProvider constraints
            if (orderInfo.PaymentProvider != null)
            {
                var paymentProvider = Providers.Instance.GetPaymentProvider(orderInfo.PaymentProvider.Key, store);

                if (paymentProvider == null)
                {
                    _logger.LogError(
                        "Unable to find matching shipping provider {PaymentProviderKey} for Order {UniqueId} ",
                        orderInfo.PaymentProvider.Key,
                        orderInfo.UniqueId);
                }

                // In case of deletion
                if (paymentProvider == null
                    || !paymentProvider.Constraints.IsValid(countryCode, total))
                {
                    _logger.LogDebug(
                        "Removing invalid payment provider {PaymentProviderKey} from Order {UniqueId}",
                        orderInfo.PaymentProvider.Key,
                        orderInfo.UniqueId);

                    orderInfo.PaymentProvider = null;
                }
            }

            // Verify shipping provider constraints
            if (orderInfo.ShippingProvider == null) return;
                
            var shippingProvider = Providers.Instance.GetShippingProvider(orderInfo.ShippingProvider.Key, store);

            if (shippingProvider == null)
            {
                _logger.LogError(
                    "Unable to find matching shipping provider {ShippingProviderKey} for Order {UniqueId} ",
                    orderInfo.ShippingProvider.Key,
                    orderInfo.UniqueId);
            }

            if (shippingProvider != null
                && shippingProvider.Constraints.IsValid(shippingCountry, total)) return;
                    
            _logger.LogDebug(
                "Removing invalid shipping provider {ShippingProviderKey} from Order {UniqueId}",
                orderInfo.ShippingProvider.Key,
                orderInfo.UniqueId);
            orderInfo.ShippingProvider = null;
        }

        private Guid GetOrderIdFromCookie(string key)
        {
            string? cookie = null;

            cookie = _httpCtx.Response
                .GetTypedHeaders()
                .SetCookie.FirstOrDefault(x => x.Name == key)?.Value.ToString();

            if (string.IsNullOrEmpty(cookie))
            {
                cookie = _httpCtx.Request.Cookies[key];
            }

            if (!string.IsNullOrEmpty(cookie))
            {
                return new Guid(cookie);
            }

            return Guid.Empty;
        }

        private Guid CreateOrderIdCookie(string key)
        {
            var guid = Guid.NewGuid();

            _httpCtx.Response.Cookies.Append(key, guid.ToString(), new CookieOptions
            {
                Expires = DateTime.UtcNow.AddDays(_config.BasketCookieLifetime)
            });

            return guid;
        }

        private void DeleteOrderCookie(string key)
        {
            _httpCtx.Response.Cookies.Delete(key);
        }

        private string GenerateOrderNumberTemplate(int referenceId, IStore store)
        {
            var _referenceId = referenceId.ToString();

            if (string.IsNullOrEmpty(store.OrderNumberTemplate))
            {
                return $"{store.OrderNumberPrefix}{referenceId:0000}";
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

        private string CreateKey(IStore store)
        {
            var key = "ekmOrder";

            if (!store.ShareBasketBetweenStores)
            {
                key += "-" + store.Alias;
            }

            return key;
        }

        /// See comments for service and under <see cref="OrderSettings"/>
        private SemaphoreSlim GetOrderLock(IOrderInfo orderInfo)
            => _orderLocks.GetOrAdd(orderInfo.UniqueId, new SemaphoreSlim(1, 1));

        /// See comments for service and under <see cref="OrderSettings"/>
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _orderLocks
            = new();
    }
}
