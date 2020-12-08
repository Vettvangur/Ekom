using Ekom.Models.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        Task<List<StockData>> GetAllStockAsync();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniqueId">
        /// Expects a value in the format
        /// $"{storeAlias}_{uniqueId}" for PerStore Stock
        /// Guid otherwise
        /// </param>
        /// <returns></returns>
        Task<StockData> GetStockByUniqueIdAsync(string uniqueId);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniqueId">
        /// Expects a value in the format
        /// $"{storeAlias}_{uniqueId}" for PerStore Stock
        /// Guid otherwise
        /// </param>
        /// <returns></returns>
        Task<StockData> CreateNewStockRecordAsync(string uniqueId);

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
        Task<int> SetAsync(string uniqueId, int modifyAmount, int oldValue);

        /// <summary>
        /// Rollback scheduled stock reservation.
        /// </summary>
        /// <param name="jobId"></param>
        /// <exception cref="StockException"></exception>
        Task RollBackJob(string jobId);
    }
}
