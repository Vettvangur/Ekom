using Ekom.Models.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Stock for coupons and discounts
    /// </summary>
    public interface IDiscountStockRepository
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniqueId">
        /// Expects a value in the format
        /// $"{uniqueId}_{coupon}" for coupon Stock
        /// Discount Guid otherwise
        /// </param>
        /// <returns></returns>
        Task<DiscountStockData> CreateNewStockRecordAsync(string uniqueId);
        /// <summary>
        /// Yes, all records in db
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<DiscountStockData>> GetAllStockAsync();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniqueId">
        /// Expects a value in the format
        /// $"{uniqueId}_{coupon}" for coupon Stock
        /// Discount Guid otherwise
        /// </param>
        /// <returns></returns>
        Task<DiscountStockData> GetStockByUniqueIdAsync(string uniqueId);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniqueId">
        /// Expects a value in the format
        /// $"{uniqueId}_{coupon}" for coupon Stock
        /// Discount Guid otherwise
        /// </param>
        /// <param name="value">
        /// Value will be added to current stock, negative numbers to decrement
        /// </param>
        /// <returns></returns>
        Task UpdateAsync(string uniqueId, int value);
    }
}
