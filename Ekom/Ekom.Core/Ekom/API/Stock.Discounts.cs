using Ekom.Models;

namespace Ekom.API;

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
        DiscountStockData stockData = await GetDiscountStockDataAsync(key, coupon)
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
        string id = coupon == null ? key.ToString() : $"{key}_{coupon}";

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
        string id = coupon == null ? key.ToString() : $"{key}_{coupon}";

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
    /// Expiry is persisted in SQL, including the exact coupon identity.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value">Only accepts negative values to indicate amount of stock to decrement</param>
    /// <param name="coupon">Leave empty to update discount master stock</param>
    /// <param name="timeSpan">How long to reserve, if unspecified, uses appSettings or Ekom default</param>
    /// <returns>Reservation ID</returns>
    public async Task<string> ReserveDiscountStockAsync(Guid key, int value, string coupon = null, TimeSpan timeSpan = default(TimeSpan))
    {
        if (value >= 0) throw new ArgumentOutOfRangeException();
        var result = await _reservations.ReserveAsync(new StockReservationRequest
        {
            Key = key, Quantity = -(decimal)value, IsDiscount = true, Coupon = coupon, Duration = timeSpan,
        }).ConfigureAwait(false);
        return RequireReservation(result);
    }

    /// <summary>
    /// Synchronous compatibility wrapper for direct discount stock increments.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public static void UpdateDiscountStockHangfire(Guid key, int value)
    {
        Instance.UpdateDiscountStockAsync(key, value).ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
