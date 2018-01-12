using Ekom.Models.Data;
using System;

namespace Ekom.API
{
    /// <summary>
    /// The Ekom API, get/update stock for item
    /// </summary>
    public partial class Stock
    {
        /// <summary>
        /// Gets stock amount from store cache. 
        /// If no stock entry exists, creates a new one.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="coupon">Leave empty to get discount master stock</param>
        /// <returns></returns>
        public int GetDiscountStock(Guid key, string coupon = null)
        {
            return GetDiscountStockData(key, coupon).Stock;
        }

        /// <summary>
        /// Gets <see cref="DiscountStockData"/> from store cache. 
        /// If no stock entry exists, creates a new one.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="coupon">Leave empty to get discount master stock</param>
        /// <returns></returns>
        public DiscountStockData GetDiscountStockData(Guid key, string coupon = null)
        {
            var id = coupon == null ? key.ToString() : $"{key}_{coupon}";

            return GetDiscountStockData(id);
        }

        /// <summary>
        /// Gets <see cref="DiscountStockData"/> from store cache. 
        /// If no stock entry exists, creates a new one.
        /// </summary>
        /// <param name="uniqueId"></param>
        /// <returns></returns>
        public DiscountStockData GetDiscountStockData(string uniqueId)
            => _discountStockRepo.GetStockByUniqueId(uniqueId);

        /// <summary>
        /// Updates stock count of store item. 
        /// If no stock entry exists, creates a new one, the attempts to update.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">Increment or decrement stock by this value</param>
        /// <param name="coupon">Leave empty to update discount master stock</param>
        /// <exception cref="ArgumentException">
        /// Throws an exception when value == 0
        /// </exception>
        /// <returns></returns>
        public void UpdateDiscountStock(Guid key, int value, string coupon = null)
        {
            var id = coupon == null ? key.ToString() : $"{key}_{coupon}";

            UpdateDiscountStock(id, value);
        }

        /// <summary>
        /// Updates stock count of store item. 
        /// If no stock entry exists, creates a new one, the attempts to update.
        /// </summary>
        /// <param name="uniqueId"></param>
        /// <param name="value">Increment or decrement stock by this value</param>
        /// <exception cref="ArgumentException">
        /// Throws an exception when value == 0
        /// </exception>
        /// <returns></returns>
        public void UpdateDiscountStock(string uniqueId, int value)
        {
            if (value == 0)
            {
                throw new ArgumentException($"Check update value, 0 triggers no change.", nameof(value));
            }

            _discountStockRepo.Update(uniqueId, value);
        }
    }
}
