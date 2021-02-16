using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Hangfire.States;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;

namespace Ekom.API
{
    // Stock reservations:
    // Vandamal med stock i dag
    // 	vid vorum ad reserve'a thad seint ad vid faum bara brot af avinningnum sem aetlunin er ad na med stock
    // 		ef vid faerum reserving i cartid thurfum vid ad utfaera order virkni 
    //      sem fylgist med hvert er elsta reservationid og nytir thad til ad birta notandanum time remaining.
    // 			
    // 	Ef vid reserve'um i payment skrefinu thurfum vid ad laga hvernig vid resettum reservationin svo notandi geti ekki eytt korfunni sinni og 
    //  nullad oll reservationin sin i einum tab medan hann chillar a greidslusidunni i odrum tab
    // 	Verdum ad laesa korfunni einhvernveginn medan notandinn er a greidslusidunni
    // 		validation getur borid saman upphaed Order og greidda upphaed en a erfitt med ad sja breytingar a vorulinum 
    //      og notandinn gaeti stutad reservations og stocki ef hann t.d. gjorbreytir korfunni sinni 
    //      en er enn med somu korfuupphaed
    // 	Draumalausn vaeri ofurutfaersla sem gaeti bodid upp a upplifunin i anda Secret Lair, thu ferd i rod ef stockid er upptekid, thegar rodin kemur ad ther reserve'um vid fyrir thig i x tima medan thu klarar kaup.
    // 		Thetta hentar best thegar thad kemur announcement um limited quantity a voru med mikla eftirspurn.
    // 		
    // Allar svona reservation lausnir virka lang best med fully managed greidsluferli thar sem notandinn greidir a sidu sem vid stjornum, 
    // thannig getum vid stoppad hann af ad greida ef timinn hans rennur ut a greidslusidunni sjalfri.
    // Annars geturu enntha lent i ad borga a sidu borgunar medan reservationid thitt rann ut a sidunni
    /// <summary>
    /// The Ekom API, get/update stock for item
    /// </summary>
    public partial class Stock
    {
        /// <summary>
        /// Stock Instance
        /// </summary>
        public static Stock Instance => Current.Factory.GetInstance<Stock>();

        readonly ILogger _logger;
        readonly Configuration _config;
        readonly IStockRepository _stockRepo;
        readonly IDiscountStockRepository _discountStockRepo;
        readonly IStoreService _storeSvc;
        readonly IBaseCache<StockData> _stockCache;
        readonly IPerStoreCache<StockData> _stockPerStoreCache;

        /// <summary>
        /// ctor
        /// </summary>
        internal Stock(
            Configuration config,
            ILogger logger,
            IBaseCache<StockData> stockCache,
            IStockRepository stockRepo,
            IDiscountStockRepository discountStockRepo,
            IStoreService storeService,
            IPerStoreCache<StockData> stockPerStoreCache
        )
        {
            _config = config;
            _stockCache = stockCache;
            _stockRepo = stockRepo;
            _discountStockRepo = discountStockRepo;
            _storeSvc = storeService;
            _stockPerStoreCache = stockPerStoreCache;

            _logger = logger;
        }

        /// <summary>
        /// Gets stock amount from cache. 
        /// If PerStoreStock is configured, gets store from cache.
        /// If no stock entry exists, creates a new one.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int GetStock(Guid key)
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
        public int GetStock(Guid key, string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("StoreAlias empty, did you mean to use GetStock(key) instead?", nameof(storeAlias));
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
                var store = _storeSvc.GetStoreFromCache();

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
        /// <returns></returns>
        public void ValidateOrderStock(IOrderInfo orderInfo)
        {
            foreach (var orderLine in orderInfo.OrderLines)
            {
                if (orderLine.Product.Backorder)
                {
                    continue;
                }

                if (orderLine.Product.VariantGroups.Any())
                {
                    foreach (var variant in orderLine.Product.VariantGroups.SelectMany(x => x.Variants))
                    {
                        var variantStock = GetStock(variant.Key);

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
                    var productStock = GetStock(orderLine.ProductKey);

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
        /// <returns></returns>
        public async Task IncrementStockAsync(Guid key, int value)
        {
            if (_config.PerStoreStock)
            {
                var store = _storeSvc.GetStoreFromCache();
                await IncrementStockAsync(key, store.Alias, value)
                    .ConfigureAwait(false);
            }
            else
            {
                await IncrementStockAsync(key, null, value)
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
        /// <returns></returns>
        /// <exception cref="NotEnoughStockException"></exception>
        public async Task IncrementStockAsync(Guid key, string storeAlias, int value)
        {
            SemaphoreSlim semaphore = null;
            StockData stockData;

            try
            {
                if (_config.PerStoreStock)
                {
                    semaphore = GetStockLock(CreateStockUniqueId(key, storeAlias));
                    await semaphore.WaitAsync().ConfigureAwait(false);

                    await EnsurePerStoreEntryExistsAsync(key, storeAlias)
                        .ConfigureAwait(false);

                    stockData = _stockPerStoreCache.Cache[storeAlias][key];
                }
                else
                {
                    semaphore = GetStockLock(CreateStockUniqueId(key));
                    await semaphore.WaitAsync().ConfigureAwait(false);

                    await EnsureStockEntryExistsAsync(key).ConfigureAwait(false);

                    stockData = _stockCache.Cache[key];
                }

                if (stockData.Stock + value < 0)
                {
                    throw new NotEnoughStockException($"Not enough stock available for {stockData.UniqueId}.");
                }

                await SetStockWithLockAsync(stockData, stockData.Stock + value, outerLock: true)
                    .ConfigureAwait(false);
            }
            finally
            {
                semaphore?.Release();
            }
        }

        /// <summary>
        /// Sets stock count of item. 
        /// If PerStoreStock is configured, gets store from cache and updates relevant item.
        /// If no stock entry exists, creates a new one, then attempts to update.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [Obsolete("Prefer Increment stock unless you have your own locks in place or are replacing stock without regard for previous state")]
        public async Task<bool> SetStockAsync(Guid key, int value)
        {
            if (_config.PerStoreStock)
            {
                var store = _storeSvc.GetStoreFromCache();
                return await SetStockAsync(key, store.Alias, value).ConfigureAwait(false);
            }
            else
            {
                return await SetStockAsync(key, null, value).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Sets stock count of store item. 
        /// If no stock entry exists, creates a new one, then attempts to update.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="storeAlias"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [Obsolete("Prefer Increment stock unless you have your own locks in place or are replacing stock without regard for previous state")]
        public async Task<bool> SetStockAsync(Guid key, string storeAlias, int value)
        {
            StockData stockData;

            if (_config.PerStoreStock)
            {
                await EnsurePerStoreEntryExistsAsync(key, storeAlias)
                    .ConfigureAwait(false);

                stockData = _stockPerStoreCache.Cache[storeAlias][key];
            }
            else
            {
                await EnsureStockEntryExistsAsync(key)
                    .ConfigureAwait(false);

                stockData = _stockCache.Cache[key];
            }

            return await SetStockWithLockAsync(stockData, value).ConfigureAwait(false);
        }

        /// <summary>
        /// Reserve stock for the given timespan.
        /// Rollback is scheduled using Hangfire
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">Only accepts negative values to indicate amount of stock to decrement</param>
        /// <param name="timeSpan">How long to reserve</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns>Hangfire Job Id</returns>
        public async Task<string> ReserveStockAsync(Guid key, int value, TimeSpan timeSpan = default)
        {
            if (value >= 0) throw new ArgumentOutOfRangeException(nameof(value), "Reserve stock called with non-negative value");
            if (timeSpan == default(TimeSpan))
            {
                timeSpan = _config.ReservationTimeout;
            }

            await IncrementStockAsync(key, value).ConfigureAwait(false);

            var jobId = Hangfire.BackgroundJob.Schedule(() =>
                UpdateStockHangfireAsync(key, -value),
                timeSpan
            );

            return jobId;
        }

        /// <summary>
        /// Reserve stock for the given timespan.
        /// Rollback is scheduled using Hangfire
        /// </summary>
        /// <param name="key"></param>
        /// <param name="storeAlias"></param>
        /// <param name="value">Only accepts negative values to indicate amount of stock to decrement</param>
        /// <param name="timeSpan">How long to reserve</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns>Hangfire Job Id</returns>
        public async Task<string> ReserveStockAsync(Guid key, string storeAlias, int value, TimeSpan timeSpan = default)
        {
            if (value >= 0) throw new ArgumentOutOfRangeException(nameof(value), "Reserve stock called with non-negative value");
            if (timeSpan == default)
            {
                timeSpan = _config.ReservationTimeout;
            }

            await IncrementStockAsync(key, storeAlias, value)
                .ConfigureAwait(false);

            var jobId = Hangfire.BackgroundJob.Schedule(() =>
                UpdateStockHangfireAsync(key, storeAlias, -value),
                timeSpan
            );

            return jobId;
        }

        /// <summary>
        /// Cancel a previously scheduled stock reservation rollback.
        /// </summary>
        /// <param name="jobId"></param>
        /// <exception cref="StockException"></exception>
        public void CancelRollback(string jobId)
        {
            if (!Hangfire.BackgroundJob.Delete(jobId, ScheduledState.StateName)
            && !Hangfire.BackgroundJob.Delete(jobId, EnqueuedState.StateName))
            {
                throw new StockException(
                    "Unable to cancel rollback job, most likely the job has already finished"
                );
            }
        }

        /// <summary>
        /// Rollback scheduled stock reservation.
        /// </summary>
        /// <param name="jobId"></param>
        /// <exception cref="StockException"></exception>
        public async Task RollbackJobAsync(string jobId)
        {
            await _stockRepo.RollBackJob(jobId).ConfigureAwait(false);

            Hangfire.BackgroundJob.Delete(jobId, ScheduledState.StateName);
            Hangfire.BackgroundJob.Delete(jobId, EnqueuedState.StateName);
        }

        /// <summary>
        /// Only requeues from scheduled state.
        /// Never requeues succeeded jobs.
        /// </summary>
        /// <param name="jobId">Hangfire Job Id</param>
        public void CompleteRollback(string jobId)
        {
            Hangfire.BackgroundJob.Requeue(
                jobId,
                ScheduledState.StateName
            );
        }

        private async Task EnsureStockEntryExistsAsync(Guid key)
        {
            if (!_stockCache.Cache.ContainsKey(key))
            {
                _stockCache.Cache[key]
                    = await _stockRepo.CreateNewStockRecordAsync(CreateStockUniqueId(key))
                    .ConfigureAwait(false);
            }
        }

        private async Task EnsurePerStoreEntryExistsAsync(Guid key, string storeAlias)
        {
            if (!_stockPerStoreCache.Cache[storeAlias].ContainsKey(key))
            {
                _stockPerStoreCache.Cache[storeAlias][key]
                    = await _stockRepo.CreateNewStockRecordAsync(CreateStockUniqueId(key, storeAlias))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Sets stock of provided <see cref="StockData"/> item and updates database.
        /// Ensures proper locking while change is performed.
        /// </summary>
        /// <param name="stockData"></param>
        /// <param name="value"></param>
        /// <param name="outerLock">True when locking is configured around this method</param>
        /// <exception cref="ArgumentException">
        /// Throws an exception when current value and provided value are equal
        /// </exception>
        /// <exception cref="ArgumentNullException"/>
        private async Task<bool> SetStockWithLockAsync(StockData stockData, int value, bool outerLock = false)
        {
            if (stockData == null)
            {
                throw new ArgumentNullException(nameof(stockData));
            }
            if (stockData.Stock == value)
            {
                return true;
            }

            //if (stockData.Stock == value)
            //{
            //    throw new ArgumentException($"Stock is already set to provided value.", nameof(value));
            //}
            if (value < 0)
            {
                throw new ArgumentException($"Cannot set stock of {stockData.UniqueId} to negative number.", nameof(value));
            }

            var semaphore = GetStockLock(stockData.UniqueId);
            if (!outerLock)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                var oldValue = stockData.Stock;
                stockData.Stock = value;

                await _stockRepo.SetAsync(stockData.UniqueId, value, oldValue).ConfigureAwait(false);

                return true;
            }
            finally
            {
                if (!outerLock)
                {
                    semaphore.Release();
                }
            }
        }

        /// <summary>
        /// Allows hangfire to serialise the method call to database
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static Task UpdateStockHangfireAsync(Guid key, int value)
        {
            return Instance.IncrementStockAsync(key, value);
        }

        /// <summary>
        /// Allows hangfire to serialise the method call to database
        /// </summary>
        /// <param name="key"></param>
        /// <param name="storeAlias"></param>
        /// <param name="value"></param>
        public static Task UpdateStockHangfireAsync(Guid key, string storeAlias, int value)
        {
            return Instance.IncrementStockAsync(key, storeAlias, value);
        }

        private string CreateStockUniqueId(Guid key) => key.ToString();
        private string CreateStockUniqueId(Guid key, string storeAlias) => $"{storeAlias}_{key}";

        private SemaphoreSlim GetStockLock(string stockData)
            => _stockLocks.GetOrAdd(stockData, new SemaphoreSlim(1, 1));
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _stockLocks
            = new ConcurrentDictionary<string, SemaphoreSlim>();
    }
}
