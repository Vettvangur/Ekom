using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Hangfire.States;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;

namespace Ekom.API
{
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
            StockData stockData;
            if (_config.PerStoreStock)
            {
                await EnsurePerStoreEntryExistsAsync(key, storeAlias)
                    .ConfigureAwait(false);

                stockData = _stockPerStoreCache.Cache[storeAlias][key];
            }
            else
            {
                await EnsureStockEntryExistsAsync(key).ConfigureAwait(false);

                stockData = _stockCache.Cache[key];
            }

            if (stockData.Stock + value < 0)
            {
                throw new NotEnoughStockException($"Not enough stock available for {stockData.UniqueId}.");
            }

            SetStockWithLock(stockData, stockData.Stock + value);
        }

        /// <summary>
        /// Sets stock count of item. 
        /// If PerStoreStock is configured, gets store from cache and updates relevant item.
        /// If no stock entry exists, creates a new one, then attempts to update.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
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

            return SetStockWithLock(stockData, value);
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
        public async Task<string> ReserveStockAsync(Guid key, int value, TimeSpan timeSpan = default(TimeSpan))
        {
            if (value >= 0) throw new ArgumentOutOfRangeException();
            if (timeSpan == default(TimeSpan))
            {
                timeSpan = _config.ReservationTimeout;
            }

            await IncrementStockAsync(key, value).ConfigureAwait(false);

            var jobId = Hangfire.BackgroundJob.Schedule(() =>
                UpdateStockHangfire(key, -value),
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
        public async Task<string> ReserveStockAsync(Guid key, string storeAlias, int value, TimeSpan timeSpan = default(TimeSpan))
        {
            if (value >= 0) throw new ArgumentOutOfRangeException();
            if (timeSpan == default)
            {
                timeSpan = _config.ReservationTimeout;
            }

            await IncrementStockAsync(key, storeAlias, value)
                .ConfigureAwait(false);

            var jobId = Hangfire.BackgroundJob.Schedule(() =>
                UpdateStockHangfire(key, storeAlias, -value),
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
                    = await _stockRepo.CreateNewStockRecordAsync(key.ToString())
                    .ConfigureAwait(false);
            }
        }

        private async Task EnsurePerStoreEntryExistsAsync(Guid key, string storeAlias)
        {
            if (!_stockPerStoreCache.Cache[storeAlias].ContainsKey(key))
            {
                _stockPerStoreCache.Cache[storeAlias][key]
                    = await _stockRepo.CreateNewStockRecordAsync($"{storeAlias}_{key}")
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Sets stock of provided <see cref="StockData"/> item and updates database.
        /// Ensures proper locking while change is performed.
        /// </summary>
        /// <param name="stockData"></param>
        /// <param name="value"></param>
        /// <exception cref="ArgumentException">
        /// Throws an exception when current value and provided value are equal
        /// </exception>
        /// <exception cref="ArgumentNullException"/>
        private bool SetStockWithLock(StockData stockData, int value)
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

            lock (stockData)
            {
                var oldValue = stockData.Stock;
                stockData.Stock = value;

                _stockRepo.SetAsync(stockData.UniqueId, value, oldValue).Wait();

                return true;
            }
        }

        /// <summary>
        /// Allows hangfire to serialise the method call to database
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void UpdateStockHangfire(Guid key, int value)
        {
            Instance.IncrementStockAsync(key, value).Wait();
        }

        /// <summary>
        /// Allows hangfire to serialise the method call to database
        /// </summary>
        /// <param name="key"></param>
        /// <param name="storeAlias"></param>
        /// <param name="value"></param>
        public static void UpdateStockHangfire(Guid key, string storeAlias, int value)
        {
            Instance.IncrementStockAsync(key, storeAlias, value).Wait();
        }
    }
}
