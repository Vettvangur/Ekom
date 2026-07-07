using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ekom.API;

/// <summary>
/// The Ekom API, get/update/remove operations on orders 
/// </summary>
public partial class Order
{
    /// <summary>
    /// Order Instance
    /// </summary>
    public static Order Instance
    {
        get
        {
            var requestServices = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext?.RequestServices;

            return requestServices?.GetService<Order>() ?? Configuration.Resolver.GetService<Order>();
        }
    }

    readonly ILogger<Order> _logger;
    readonly Configuration _config;
    readonly DiscountCache _discountCache;
    readonly ICouponCache _couponCache;
    readonly OrderService _orderService;
    readonly CheckoutService _checkoutService;
    readonly IStoreService _storeSvc;
    readonly OrderRepository _orderRepo;
    readonly CheckoutControllerService _checkoutControllerService;
    readonly IOrderActivityLogService _orderActivityLogService;

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
        IStoreService storeService,
        OrderRepository orderRepo,
        CheckoutControllerService checkoutControllerService,
        IOrderActivityLogService orderActivityLogService)
    {
        _discountCache = discountCache;
        _orderService = orderService;
        _checkoutService = checkoutService;
        _storeSvc = storeService;
        _couponCache = couponCache;
        _config = config;
        _logger = logger;
        _orderRepo = orderRepo;
        _checkoutControllerService = checkoutControllerService;
        _orderActivityLogService = orderActivityLogService;
    }

    public IOrderInfo? GetOrder() => GetOrderAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Get order using cookie data and ekmRequest store.
    /// Retrieves from session if possible, otherwise from SQL.
    /// </summary>
    /// <returns></returns>
    public Task<IOrderInfo?> GetOrderAsync(CancellationToken ct = default)
    {
        IStore? store = _storeSvc.GetStoreFromCache();

        if (store == null)
        {
            return Task.FromResult<IOrderInfo?>(null);
        }

        return GetOrderAsync(store.Alias, ct);
    }

    public IOrderInfo? GetOrder(string storeAlias) => GetOrderAsync(storeAlias).GetAwaiter().GetResult();

    /// <summary>
    /// Get order using cookie data and provided store.
    /// Retrieves from session if possible, otherwise from SQL.
    /// </summary>
    /// <returns></returns>
    public async Task<IOrderInfo?> GetOrderAsync(string? storeAlias, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            return await GetOrderAsync(ct);
        }

        return await _orderService.GetOrderAsync(storeAlias, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Get order regardless of status using <see cref="Guid"/>.
    /// This may return complete / final orders. <br />
    /// Do not use for cart or checkout display.
    /// </summary>
    /// <returns></returns>
    public IOrderInfo? GetOrder(Guid uniqueId) => _orderService.GetOrderAsync(uniqueId).GetAwaiter().GetResult();

    /// <summary>
    /// Get order regardless of status using <see cref="Guid"/>.
    /// This may return complete / final orders. <br />
    /// Do not use for cart or checkout display.
    /// </summary>
    /// <returns></returns>
    public async Task<IOrderInfo?> GetOrderAsync(Guid uniqueId, CancellationToken ct = default) => await _orderService.GetOrderAsync(uniqueId, ct);

    /// <summary>
    /// Get completed order using cookie data and provided store.
    /// Retrieves from session if possible, otherwise from SQL.
    /// </summary>
    /// <returns></returns>
    public IOrderInfo? GetCompletedOrder(string storeAlias) => GetCompletedOrderAsync(storeAlias).GetAwaiter().GetResult();

    /// <summary>
    /// Get completed order using cookie data and provided store.
    /// Retrieves from session if possible, otherwise from SQL.
    /// </summary>
    /// <returns></returns>
    public async Task<IOrderInfo?> GetCompletedOrderAsync(string storeAlias, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException($"The value for '{nameof(storeAlias)}' must be specified");
        }

        return await _orderService.GetCompletedOrderAsync(storeAlias, ct)
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
        CancellationToken ct = default,
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

        List<OrderInfo> orders = await _orderService.GetStatusOrdersByCustomerUsernameAsync(customerUsername, ct, orderStatuses)
            .ConfigureAwait(false);

        return orders.Cast<IOrderInfo>();
    }

    /// <summary>
    /// Get all orders with the given <see cref="OrderStatus"/> for the given customer
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="orderStatuses"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IEnumerable<IOrderInfo>> GetStatusOrdersByCustomerIdAsync(
        int customerId,
        CancellationToken ct = default,
        params OrderStatus[] orderStatuses
    )
    {
        if (!orderStatuses.Any())
        {
            throw new ArgumentException("At least one OrderStatus must be specified");
        }

        List<OrderInfo> orders = await _orderService.GetStatusOrdersByCustomerIdAsync(customerId, ct, orderStatuses)
            .ConfigureAwait(false);

        return orders.Cast<IOrderInfo>();
    }

    /// <summary>
    /// Get all orders for logged in user with the given <see cref="OrderStatus"/>
    /// </summary>
    /// <param name="orderStatuses"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IEnumerable<IOrderInfo>> GetStatusOrdersByCustomerIdAsync(
        CancellationToken ct = default,
        params OrderStatus[] orderStatuses
    )
    {
        if (!orderStatuses.Any())
        {
            throw new ArgumentException("At least one OrderStatus must be specified");
        }

        List<OrderInfo> orders = await _orderService.GetStatusOrdersByCustomerIdAsync(ct, orderStatuses)
            .ConfigureAwait(false);

        return orders.Cast<IOrderInfo>();
    }

    /// <summary>
    /// Get all orders with the given <see cref="OrderStatus"/>
    /// </summary>
    /// <param name="orderStatuses"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IEnumerable<IOrderInfo>> GetStatusOrdersAsync(
        CancellationToken ct = default,
        params OrderStatus[] orderStatuses
    )
    {
        if (!orderStatuses.Any())
        {
            throw new ArgumentException("At least one OrderStatus must be specified");
        }

        List<OrderInfo> orders = await _orderService.GetStatusOrdersAsync(ct, orderStatuses)
            .ConfigureAwait(false);

        return orders.Cast<IOrderInfo>();
    }


    /// <summary>
    /// Completed orders with <see cref="OrderStatus"/> in one of the last stages
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="storeAlias"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IEnumerable<IOrderInfo>> GetCompleteCustomerOrdersAsync(int customerId, CancellationToken ct = default, string? storeAlias = null)
    {

        return await _orderService.GetCompleteCustomerOrdersAsync(customerId, ct, storeAlias)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Completed orders with <see cref="OrderStatus"/> in one of the last stages
    /// </summary>
    /// <param name="userName"></param>
    /// <param name="storeAlias"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IEnumerable<IOrderInfo>> GetCompleteCustomerOrdersAsync(string userName, CancellationToken ct = default, string? storeAlias = null)
    {
        return await _orderService.GetCompleteCustomerOrdersAsync(userName, ct, storeAlias)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="OrderInfoNotFoundException"></exception>
    public async Task UpdateStatusAsync(string storeAlias, OrderStatus newStatus, ChangeOrderSettings? settings = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
        }

        OrderInfo? orderInfo;
        if (settings?.OrderInfo == null)
        {
            orderInfo = await _orderService.GetOrderAsync(storeAlias, ct).ConfigureAwait(false);
        }
        else
        {
            orderInfo = settings.OrderInfo as OrderInfo;
        }

        if (orderInfo == null)
        {
            throw new OrderInfoNotFoundException();
        }

        await _orderService.ChangeOrderStatusAsync(orderInfo.UniqueId, newStatus, null, settings, ct = ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <exception cref="OrderInfoNotFoundException">OrderData not found for given OrderId</exception>
    public async Task UpdateStatusAsync(OrderStatus newStatus, Guid orderId, string? userName = null, ChangeOrderSettings? settings = null, CancellationToken ct = default)
    {
        await _orderService.ChangeOrderStatusAsync(orderId, newStatus, userName, settings, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Add an activity log entry to an existing order.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when orderId or message is invalid.</exception>
    /// <exception cref="OrderInfoNotFoundException">Thrown when no order exists for the provided orderId.</exception>
    public async Task AddActivityLogAsync(
        Guid orderId,
        string message,
        string? userName = null,
        OrderActivityLogType logType = OrderActivityLogType.Info,
        CancellationToken ct = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id cannot be empty.", nameof(orderId));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));
        }

        OrderInfo? orderInfo = await _orderService.GetOrderAsync(orderId, ct).ConfigureAwait(false);

        if (orderInfo == null)
        {
            throw new OrderInfoNotFoundException();
        }

        await _orderActivityLogService.AddOrderLogAsync(orderId, message, userName, logType, ct)
            .ConfigureAwait(false);
    }

    public Task<IOrderInfo?> UpdateCurrencyAsync(string currency, Guid orderId, string storeAlias, CancellationToken ct = default)
    {
        return _orderService.ChangeCurrencyAsync(orderId, currency, storeAlias, ct);
    }

    /// <summary>
    /// 
    /// </summary>
    public async Task<IOrderInfo> ReInitializeOrder(string storeAlias, OrderSettings? settings = null, CancellationToken ct = default)
    {
        return await _orderService.ReInitializeOrderLinesAsync(storeAlias, settings, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Add order line to cart asynchronously.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The quantity.</param>
    /// <param name="storeAlias">The store alias.</param>
    /// <param name="settings">Ekom Order Api AddOrderLine optional configuration</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">storeAlias</exception>
    /// <exception cref="OrderLineNegativeException">Can indicate a request to modify lines to negative values f.x. </exception>
    /// <exception cref="ProductNotFoundException"></exception>
    /// <exception cref="VariantNotFoundException"></exception>
    /// <exception cref="NotEnoughStockException"></exception>
    public async Task<IOrderInfo> AddOrderLineAsync(
        Guid productId,
        decimal quantity,
        string storeAlias,
        AddOrderSettings? settings = null,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            IStore? store = _storeSvc.GetStoreFromCache();

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
            settings: settings ?? new AddOrderSettings(),
            ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 
    /// </summary>
    public async Task<IOrderInfo> UpdateCustomerInformationAsync(
        Dictionary<string, string> form,
        OrderSettings? settings = null,
        CancellationToken ct = default)
    {
        if (form == null)
        {
            throw new ArgumentNullException(nameof(form));
        }

        return await _orderService.UpdateCustomerInformationAsync(form, settings ?? null, ct: ct)
            .ConfigureAwait(false);
    }

    public async Task<IOrderInfo> UpdateTrackingAsync(
        string storeAlias,
        OrderTracking tracking,
        OrderSettings? settings = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
        {
            throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
        }

        return await _orderService.UpdateTrackingAsync(storeAlias, tracking, settings, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 
    /// </summary>
    public async Task<IOrderInfo> UpdateShippingInformationAsync(
        Guid shippingProvider,
        string storeAlias,
        Dictionary<string, string> allData,
        OrderSettings? settings = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
        }

        return await _orderService.UpdateShippingInformationAsync(shippingProvider, storeAlias, allData, settings, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 
    /// </summary>
    public async Task<IOrderInfo> UpdatePaymentInformationAsync(
        Guid paymentProvider,
        string storeAlias,
        Dictionary<string, string> allData,
        OrderSettings? settings = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
        }

        return await _orderService.UpdatePaymentInformationAsync(paymentProvider, storeAlias, allData, settings, ct)
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
        RemoveOrderSettings? settings = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
        }

        return await _orderService.RemoveOrderLineProductAsync(productKey, storeAlias, settings ?? null, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="lineId"></param>
    /// <param name="storeAlias">The store alias.</param>
    /// <param name="settings">Ekom Order Api optional configuration</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    public async Task<IOrderInfo> RemoveOrderLineAsync(
        Guid lineId,
        string storeAlias,
        OrderSettings? settings = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
        }

        return await _orderService.RemoveOrderLineAsync(lineId, storeAlias, settings, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Sets quantity to specified amount
    /// </summary>
    /// <param name="lineId"></param>
    /// <param name="quantity"></param>
    /// <param name="storeAlias"></param>
    /// <param name="settings"></param>
    /// <param name="ct">CancellationToken</param>
    /// <exception cref="OrderInfoNotFoundException"></exception>
    /// <exception cref="OrderLineNotFoundException"></exception>
    /// <exception cref="NotEnoughStockException"></exception>
    /// <returns></returns>
    public async Task<IOrderInfo> UpdateOrderlineQuantityAsync(
        Guid lineId,
        decimal quantity,
        string storeAlias,
        OrderSettings? settings = null,
        CancellationToken ct = default)
    {
        return await _orderService.UpdateOrderLineQuantityAsync(lineId, quantity, storeAlias, settings, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 
    /// </summary>
    public async Task CompleteOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        await _checkoutService.CompleteAsync(orderId, ct)
            .ConfigureAwait(false);
    }

    public async Task ClearCustomerOrderReferenceAsync(Guid orderId, OrderData? order = null, CancellationToken ct = default)
    {
        order = order == null ? await _orderRepo.GetOrderAsync(orderId, ct).ConfigureAwait(false) : order;

        _orderService.ClearCustomerOrderReference(order);
    }

    /// <summary>
    /// Save multiple hangfire job ids to <see cref="IOrderInfo"/> and db
    /// </summary>
    /// <param name="hangfireJobs">Job IDs to add</param>
    /// <param name="orderInfo">orderInfo</param>
    /// <param name="storeAlias">storeAlias</param>
    /// <param name="ct">CancellationToken</param>
    public async Task AddHangfireJobsToOrderAsync(IEnumerable<string> hangfireJobs, IOrderInfo orderInfo, string? storeAlias = null, CancellationToken ct = default)
    {
        if (hangfireJobs == null)
        {
            throw new ArgumentNullException(nameof(hangfireJobs));
        }

        if (string.IsNullOrEmpty(storeAlias))
        {
            IStore? store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                storeAlias = store.Alias;
            }
        }
        if (orderInfo == null)
        {
            throw new ArgumentNullException("OrderInfo is null", nameof(orderInfo));
        }
        await AddHangfireJobsToOrderAsync(storeAlias, hangfireJobs, orderInfo, ct: ct)
            .ConfigureAwait(false);
    }
    /// <summary>
    /// Save multiple hangfire job ids to <see cref="IOrderInfo"/> and db
    /// </summary>
    /// <param name="storeAlias"></param>
    /// <param name="hangfireJobs">Job IDs to add</param>
    /// <param name="orderInfo">orderInfo</param>
    /// <param name="ct">CancellationToken</param>
    public async Task AddHangfireJobsToOrderAsync(string storeAlias, IEnumerable<string> hangfireJobs, IOrderInfo orderInfo, CancellationToken ct = default)
    {
        if (hangfireJobs == null)
        {
            throw new ArgumentNullException(nameof(hangfireJobs));
        }
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException("Null or empty storeAlias", nameof(storeAlias));
        }
        if (orderInfo == null)
        {
            throw new ArgumentNullException("OrderInfo is null", nameof(orderInfo));
        }

        await _orderService.AddHangfireJobsToOrderAsync(storeAlias, hangfireJobs, orderInfo as OrderInfo, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Remove all hangfire job ids to <see cref="IOrderInfo"/> and db
    /// </summary>
    /// <param name="storeAlias"></param>
    public async Task RemoveHangfireJobsFromOrderAsync(string storeAlias, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException("string.IsNullOrEmpty", nameof(storeAlias));
        }

        await _orderService.RemoveHangfireJobsToOrderAsync(storeAlias, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Pay action to submit order to payment.
    /// <param name="paymentRequest"></param>
    /// <param name="storeAlias"></param>
    /// <param name="orderId"></param>
    public async Task<CheckoutResponse> PayAsync(PaymentRequest paymentRequest, string storeAlias, Guid orderId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException("string.IsNullOrEmpty", nameof(storeAlias));
        }

        CheckoutResponse res = await _checkoutControllerService.PayAsync(paymentRequest, "", orderId, ct)
            .ConfigureAwait(false);

        return res;
    }

    /// <summary>
    /// Pay action to submit order to payment.
    /// </summary>
    /// <param name="paymentRequest"></param>
    /// <param name="storeAlias"></param>
    /// <param name="order"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<CheckoutResponse> PayAsync(PaymentRequest paymentRequest, string storeAlias, IOrderInfo order, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException("string.IsNullOrEmpty", nameof(storeAlias));
        }

        if (order == null)
        {
            throw new ArgumentNullException(nameof(order));
        }

        CheckoutResponse res = await _checkoutControllerService.PayAsync(paymentRequest, "", order, ct)
            .ConfigureAwait(false);

        return res;
    }

    /// <summary>
    /// Delete Order cookie
    /// </summary>
    /// <param name="storeAlias"></param>
    /// <returns></returns>
    public void DeleteOrderCookie(string? storeAlias = null)
    {
        IStore? store = 
            !string.IsNullOrEmpty(storeAlias) ? 
            _storeSvc.GetStoreByAlias(storeAlias) : 
            _storeSvc.GetStoreFromCache();

        if (store != null)
        {
            _orderService.DeleteOrderCookie(store);
        }
    }

    public void EnsureOrderCookie(Guid orderId, string? storeAlias = null)
    {
        IStore? store =
            !string.IsNullOrEmpty(storeAlias) ?
            _storeSvc.GetStoreByAlias(storeAlias) :
            _storeSvc.GetStoreFromCache();

        if (store != null)
        {
            _orderService.EnsureOrderCookie(store, orderId);
        }
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
            || orderStatus.Value == OrderStatus.ReadyForPickup
            || orderStatus.Value == OrderStatus.ReadyForDispatchWhenStockArrives
            || orderStatus.Value == OrderStatus.Dispatched
            //|| orderStatus.Value == OrderStatus.WaitingForPayment
            || orderStatus.Value == OrderStatus.Returned
            || orderStatus.Value == OrderStatus.Cancelled
            || orderStatus.Value == OrderStatus.Closed
            );
}
