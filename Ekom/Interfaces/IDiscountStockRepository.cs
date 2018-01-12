using Ekom.Models.Data;
using System.Collections.Generic;

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
        DiscountStockData CreateNewStockRecord(string uniqueId);
        /// <summary>
        /// Yes, all records in db
        /// </summary>
        /// <returns></returns>
        IEnumerable<DiscountStockData> GetAllStock();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniqueId">
        /// Expects a value in the format
        /// $"{uniqueId}_{coupon}" for coupon Stock
        /// Discount Guid otherwise
        /// </param>
        /// <returns></returns>
        DiscountStockData GetStockByUniqueId(string uniqueId);
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
        void Update(string uniqueId, int value);
    }
}
