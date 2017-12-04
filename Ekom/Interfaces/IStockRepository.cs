using Ekom.Models.Data;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Handles database transactions for <see cref="StockData"/>
    /// </summary>
    public interface IStockRepository
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        IEnumerable<StockData> GetAllStock();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniqueId">
        /// Expects a value in the format
        /// $"{storeAlias}_{uniqueId}" for PerStore Stock
        /// Guid otherwise
        /// </param>
        /// <returns></returns>
        StockData GetStockByUniqueId(string uniqueId);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniqueId">
        /// Expects a value in the format
        /// $"{storeAlias}_{uniqueId}" for PerStore Stock
        /// Guid otherwise
        /// </param>
        /// <returns></returns>
        StockData CreateNewStockRecord(string uniqueId);

        /// <summary>
        /// Increment or decrement stock by the supplied value
        /// </summary>
        /// <param name="uniqueId">
        /// Expects a value in the format
        /// $"{storeAlias}_{uniqueId}" for PerStore Stock
        /// Guid otherwise
        /// </param>
        /// <param name="modifyAmount">This value can be negative or positive depending on whether the indended action is to increment or decrement stock</param>
        /// <param name="oldValue">Old stock value</param>
        /// <returns></returns>
        int Set(string uniqueId, int modifyAmount, int oldValue);
    }
}
