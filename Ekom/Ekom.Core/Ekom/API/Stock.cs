using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ekom.API;

/// <summary>
/// The Ekom API, get/update stock for item
/// </summary>
public partial class Stock
{
    /// <summary>
    /// Stock Instance
    /// </summary>}
    public static Stock Instance => Configuration.Resolver.GetService<Stock>();

    readonly ILogger<Stock> _logger;
    readonly Configuration _config;
    readonly StockRepository _stockRepo;
    readonly DiscountStockRepository _discountStockRepo;
    readonly IStoreService _storeSvc;
    readonly IBaseCache<StockData> _stockCache;
    readonly IPerStoreCache<StockData> _stockPerStoreCache;
    readonly IStockReservationService _reservations;
    readonly StockChangePublisher _stockPublisher;

    /// <summary>
    /// ctor
    /// </summary>
    internal Stock(
        Configuration config,
        ILogger<Stock> logger,
        IBaseCache<StockData> stockCache,
        StockRepository stockRepo,
        DiscountStockRepository discountStockRepo,
        IStoreService storeService,
        IPerStoreCache<StockData> stockPerStoreCache,
        IStockReservationService reservations,
        StockChangePublisher stockPublisher
    )
    {
        _config = config;
        _stockCache = stockCache;
        _stockRepo = stockRepo;
        _discountStockRepo = discountStockRepo;
        _storeSvc = storeService;
        _stockPerStoreCache = stockPerStoreCache;
        _reservations = reservations;
        _stockPublisher = stockPublisher;

        _logger = logger;
    }

    /// <summary>
    /// Gets stock amount from cache. 
    /// If PerStoreStock is configured, gets store from cache.
    /// If no stock entry exists, creates a new one.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public decimal GetStock(Guid key)
    {
        return GetStockData(key).Stock;
    }

    /// <summary>
    /// Gets stock amount from store cache. 
    /// If no stock entry exists, creates a new one.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="storeAlias"></param>
    /// <returns></returns>
    public decimal GetStock(Guid key, string storeAlias)
    {
        if (string.IsNullOrWhiteSpace(storeAlias) || !_config.PerStoreStock)
        {
            return GetStock(key);
        }

        return GetStockData(key, storeAlias).Stock;
    }

    /// <summary>
    /// Gets <see cref="StockData"/> from cache. 
    /// If PerStoreStock is configured, gets store from cache.
    /// If no stock entry exists, creates a new one.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public StockData GetStockData(Guid key)
    {
        if (_config.PerStoreStock)
        {
            IStore? store = _storeSvc.GetStoreFromCache();

            return GetStockData(key, store.Alias);
        }
        else
        {
            return _stockCache.Cache.ContainsKey(key)
            ? _stockCache.Cache[key]
            : new StockData
            {
                CreateDate = DateTime.Now,
                UpdateDate = DateTime.Now,
                Stock = 0,
                UniqueId = key.ToString(),
            };
        }
    }

    /// <summary>
    /// Gets <see cref="StockData"/> from store cache. 
    /// If no stock entry exists, creates a new one.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="storeAlias"></param>
    /// <returns></returns>
    public StockData GetStockData(Guid key, string storeAlias)
    {
        if (string.IsNullOrWhiteSpace(storeAlias) || !_config.PerStoreStock)
        {
            return GetStockData(key);
        }

        return _stockPerStoreCache.Cache.ContainsKey(storeAlias) && _stockPerStoreCache.Cache[storeAlias].ContainsKey(key)
        ? _stockPerStoreCache.Cache[storeAlias][key]
        : new StockData
        {
            CreateDate = DateTime.Now,
            UpdateDate = DateTime.Now,
            Stock = 0,
            UniqueId = $"{storeAlias}_{key}",
        };
    }

    /// <summary>
    /// Verify stock for <see cref="IOrderInfo"/>
    /// </summary>
    /// <param name="orderInfo"></param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    public async Task ValidateOrderStockAsync(IOrderInfo orderInfo, CancellationToken ct = default)
    {
        foreach (IOrderLine orderLine in orderInfo.OrderLines)
        {
            ct.ThrowIfCancellationRequested();

            if (orderLine.Product.Backorder)
            {
                continue;
            }

            IProduct? product = await Catalog.Instance.GetProductAsync(orderLine.ProductKey, orderInfo.StoreInfo.Alias, raiseEvent: false, ct: ct);

            if (product == null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            if (orderLine.Product.VariantGroups.Any())
            {
                foreach (OrderedVariant? orderedVariant in orderLine.Product.VariantGroups.SelectMany(x => x.Variants))
                {
                    IVariant? variant = await Catalog.Instance.GetVariantAsync(orderedVariant.Key, orderInfo.StoreInfo.Alias, ct: ct);

                    if (variant == null)
                    {
                        throw new ArgumentNullException(nameof(variant));
                    }

                    decimal variantStock = StockBufferHelper.GetEffectiveStock(variant.Stock, product, variant);

                    if (variantStock < orderLine.Quantity)
                    {
                        throw new NotEnoughLineStockException
                        {
                            OrderLineKey = orderLine.Key,
                            Variant = true,
                        };
                    }
                }
            }
            else
            {
                decimal productStock = StockBufferHelper.GetEffectiveStock(product.Stock, product);

                if (productStock < orderLine.Quantity)
                {
                    throw new NotEnoughLineStockException
                    {
                        OrderLineKey = orderLine.Key,
                        Variant = false,
                    };
                }
            }
        }
    }

    /// <summary>
    /// Increment stock count of item. 
    /// If PerStoreStock is configured, gets store from cache and updates relevant item.
    /// If no stock entry exists, creates a new one, then attempts to update.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    public async Task IncrementStockAsync(Guid key, decimal value, CancellationToken ct = default)
    {
        if (_config.PerStoreStock)
        {
            IStore? store = _storeSvc.GetStoreFromCache();
            await IncrementStockAsync(key, store.Alias, value, ct)
                .ConfigureAwait(false);
        }
        else
        {
            await IncrementStockAsync(key, null, value, ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Increment stock count of store item. 
    /// If no stock entry exists, creates a new one, then attempts to update.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="storeAlias"></param>
    /// <param name="value"></param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    /// <exception cref="NotEnoughStockException"></exception>
    public async Task IncrementStockAsync(Guid key, string storeAlias, decimal value, CancellationToken ct = default)
    {
        var scope = ResolveStockStore(storeAlias);
        var id = scope == null ? key.ToString() : $"{scope}_{key}";
        var oldValue = await _stockRepo.MutateAsync(id, value, increment: true, ct).ConfigureAwait(false);
        await _stockPublisher.PublishAsync(key, scope, id, oldValue, oldValue + value, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets stock count of item. 
    /// If PerStoreStock is configured, gets store from cache and updates relevant item.
    /// If no stock entry exists, creates a new one, then attempts to update.
    /// Prefer Increment stock unless you have your own locks in place or are replacing stock without regard for previous state.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> SetStockAsync(Guid key, decimal value, CancellationToken ct = default)
    {
        if (_config.PerStoreStock)
        {
            IStore? store = _storeSvc.GetStoreFromCache();
            return await SetStockAsync(key, store.Alias, value, ct).ConfigureAwait(false);
        }
        else
        {
            return await SetStockAsync(key, null, value, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sets stock count of store item. 
    /// If no stock entry exists, creates a new one, then attempts to update.
    /// Prefer Increment stock unless you have your own locks in place or are replacing stock without regard for previous state.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="storeAlias"></param>
    /// <param name="value"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> SetStockAsync(Guid key, string storeAlias, decimal value, CancellationToken ct = default)
    {
        var scope = ResolveStockStore(storeAlias);
        var id = scope == null ? key.ToString() : $"{scope}_{key}";
        var oldValue = await _stockRepo.MutateAsync(id, value, increment: false, ct).ConfigureAwait(false);
        await _stockPublisher.PublishAsync(key, scope, id, oldValue, value, ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Reserve stock for the given timespan.
    /// Expiry is persisted in SQL and processed by the native reservation worker.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value">Only accepts negative values to indicate amount of stock to decrement</param>
    /// <param name="timeSpan">How long to reserve</param>
    /// <param name="ct">Cancellation token</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <returns>Reservation ID</returns>
    public async Task<string> ReserveStockAsync(Guid key, decimal value, TimeSpan timeSpan = default, CancellationToken ct = default)
    {
        return await ReserveStockAsync(key, CheckoutPreparationScope.Current?.Order.StoreInfo.Alias ?? ResolveStockStore(null), value, timeSpan, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reserve stock for the given timespan.
    /// Expiry is persisted in SQL and processed by the native reservation worker.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="storeAlias"></param>
    /// <param name="value">Only accepts negative values to indicate amount of stock to decrement</param>
    /// <param name="timeSpan">How long to reserve</param>
    /// <param name="ct">Cancellation token</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <returns>Reservation ID</returns>
    public async Task<string> ReserveStockAsync(Guid key, string storeAlias, decimal value, TimeSpan timeSpan = default, CancellationToken ct = default)
    {
        if (value >= 0) throw new ArgumentOutOfRangeException(nameof(value), "Reserve stock called with non-negative value");
        if (CheckoutPreparationScope.Current is { } preparation)
            return await preparation.Service.ReserveLegacyAsync(preparation, new StockReservationRequest
            {
                Key = key, Quantity = -value, StoreAlias = storeAlias, Duration = timeSpan,
            }, ct).ConfigureAwait(false);
        var result = await _reservations.ReserveAsync(new StockReservationRequest
        {
            Key = key, Quantity = -value, StoreAlias = ResolveStockStore(storeAlias), Duration = timeSpan,
        }, ct).ConfigureAwait(false);
        return RequireReservation(result);
    }

    /// <summary>
    /// Cancel a previously scheduled stock reservation rollback.
    /// </summary>
    /// <param name="jobId"></param>
    /// <exception cref="StockException"></exception>
    public void CancelRollback(string jobId)
    {
        // Synchronous compatibility boundary; asynchronous callers should use the injected service.
        var result = _reservations.ConsumeAsync(jobId).ConfigureAwait(false).GetAwaiter().GetResult();
        if (result.Status is not (StockReservationStatus.Consumed or StockReservationStatus.AlreadyConsumed))
        {
            throw new StockException($"Unable to consume reservation {jobId}: {result.Status}.");
        }
    }

    /// <summary>
    /// Rollback scheduled stock reservation.
    /// </summary>
    /// <param name="jobId"></param>
    /// <param name="ct">Cancellation token</param>
    /// <exception cref="StockException"></exception>
    public async Task RollbackJobAsync(string jobId, CancellationToken ct = default)
    {
        await _reservations.ReleaseAsync(jobId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases an active reservation immediately.
    /// Terminal reservations never restore stock again.
    /// </summary>
    /// <param name="jobId">Reservation ID</param>
    public void CompleteRollback(string jobId)
    {
        _reservations.ReleaseAsync(jobId).ConfigureAwait(false).GetAwaiter().GetResult();
    }


    private string? ResolveStockStore(string? storeAlias)
    {
        if (!_config.PerStoreStock) return null;
        var alias = string.IsNullOrWhiteSpace(storeAlias) ? _storeSvc.GetStoreFromCache()?.Alias : storeAlias;
        if (string.IsNullOrWhiteSpace(alias)) throw new InvalidOperationException("A store is required for per-store stock.");
        return alias;
    }

    private static string RequireReservation(StockReservationResult result)
    {
        if (result.Status == StockReservationStatus.InsufficientStock) throw new NotEnoughStockException("Not enough stock to reserve.");
        if (result.Status is not (StockReservationStatus.Created or StockReservationStatus.AlreadyExists))
            throw new StockException($"Unable to reserve stock: {result.Status}.");
        return result.ReservationId!;
    }

    /// <summary>
    /// Compatibility wrapper for direct stock increments.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="ct">Cancellation token</param>
    public static Task UpdateStockHangfireAsync(Guid key, decimal value, CancellationToken ct)
    {
        return Instance.IncrementStockAsync(key, value, ct);
    }

    /// <summary>
    /// Compatibility wrapper for direct stock increments.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="storeAlias"></param>
    /// <param name="value"></param>
    /// <param name="ct">Cancellation token</param>
    public static Task UpdateStockHangfireAsync(Guid key, string storeAlias, decimal value, CancellationToken ct)
    {
        return Instance.IncrementStockAsync(key, storeAlias, value, ct);
    }

}
