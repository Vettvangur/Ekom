using Ekom.Models;
using System;
using System.Threading.Tasks;

namespace Ekom.API
{
    /// <summary>
    /// The Ekom API, get/update stock for item
    /// </summary>
    public partial class Stock
    {
        /// <summary>
        /// Gets stock amount from db. 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="coupon">Leave empty to get discount master stock</param>
        /// <returns></returns>
        public async Task<int> GetDiscountStockAsync(Guid key, string coupon = null)
        {
            var stockData = await GetDiscountStockDataAsync(key, coupon)
                .ConfigureAwait(false);

            return stockData.Stock;
        }

        /// <summary>
        /// Gets <see cref="DiscountStockData"/> from db. 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="coupon">Leave empty to get discount master stock</param>
        /// <returns></returns>
        public async Task<DiscountStockData> GetDiscountStockDataAsync(Guid key, string coupon = null)
        {
            var id = coupon == null ? key.ToString() : $"{key}_{coupon}";

            return await GetDiscountStockDataAsync(id).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets <see cref="DiscountStockData"/> from db. 
        /// </summary>
        /// <param name="uniqueId"></param>
        /// <returns></returns>
        public async Task<DiscountStockData> GetDiscountStockDataAsync(string uniqueId)
            => await _discountStockRepo.GetStockByUniqueIdAsync(uniqueId)
                .ConfigureAwait(false);

        /// <summary>
        /// Updates stock count of discount. 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">Increment or decrement stock by this value</param>
        /// <param name="coupon">Leave empty to update discount master stock</param>
        /// <exception cref="ArgumentException">
        /// Throws an exception when value == 0
        /// </exception>
        /// <returns></returns>
        public async Task UpdateDiscountStockAsync(Guid key, int value, string coupon = null)
        {
            var id = coupon == null ? key.ToString() : $"{key}_{coupon}";

            await UpdateDiscountStockAsync(id, value).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates stock count of discount. 
        /// </summary>
        /// <param name="uniqueId"></param>
        /// <param name="value">Increment or decrement stock by this value</param>
        /// <exception cref="ArgumentException">
        /// Throws an exception when value == 0
        /// </exception>
        /// <returns></returns>
        public async Task UpdateDiscountStockAsync(string uniqueId, int value)
        {
            if (string.IsNullOrEmpty(uniqueId))
            {
                throw new ArgumentException(nameof(uniqueId));
            }
            if (value == 0)
            {
                throw new ArgumentException($"Check update value, 0 triggers no change.", nameof(value));
            }

            await _discountStockRepo.UpdateAsync(uniqueId, value)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Reserve stock for the given timespan.
        /// Rollback is scheduled using Hangfire
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">Only accepts negative values to indicate amount of stock to decrement</param>
        /// <param name="coupon">Leave empty to update discount master stock</param>
        /// <param name="timeSpan">How long to reserve, if unspecified, uses appSettings or Ekom default</param>
        /// <returns>Hangfire Job Id</returns>
        public async Task<string> ReserveDiscountStockAsync(Guid key, int value, string coupon = null, TimeSpan timeSpan = default(TimeSpan))
        {
            if (value >= 0) throw new ArgumentOutOfRangeException();
            if (timeSpan == default(TimeSpan))
            {
                timeSpan = _config.ReservationTimeout;
            }

            await UpdateDiscountStockAsync(key, value, coupon)
                .ConfigureAwait(false);

            var jobId = Hangfire.BackgroundJob.Schedule(() =>
                UpdateDiscountStockHangfire(key, -value),
                timeSpan
            );

            return jobId;
        }

        /// <summary>
        /// Allows hangfire to serialise the method call to database
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void UpdateDiscountStockHangfire(Guid key, int value)
        {
            Instance.UpdateDiscountStockAsync(key, value).Wait();
        }
    }
}
