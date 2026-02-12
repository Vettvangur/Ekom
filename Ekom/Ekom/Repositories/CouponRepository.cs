using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Logging;

namespace Ekom.Repositories;

class CouponRepository
{
    readonly ILogger _logger;
    readonly DatabaseFactory _databaseFactory;
    readonly Configuration _config;
    readonly ICouponCache _couponCache;
    readonly INodeService _nodeService;
    /// <summary>
    /// ctor
    /// </summary>
    public CouponRepository(
        Configuration config,
        ILogger<CouponRepository> logger,
        ICouponCache couponCache,
        DatabaseFactory databaseFactory,
        INodeService nodeService)
    {
        _config = config;
        _logger = logger;
        _couponCache = couponCache;
        _databaseFactory = databaseFactory;
        _nodeService = nodeService;
    }

    public async Task InsertCouponAsync(CouponData couponData, CancellationToken ct = default)
    {
        if (!await CouponCodeExistAsync(couponData.CouponCode)
            .ConfigureAwait(false))
        {
            await using DbContext db = _databaseFactory.GetDatabase();

            await db.InsertAsync(couponData, token: ct)
                .ConfigureAwait(false);

            RefreshCache(couponData);
        }
        else
        {
            throw new ArgumentException("Duplicate coupon");
        }
    }

    public async Task UpdateCouponAsync(CouponData couponData, CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        await db.UpdateAsync(couponData, token: ct)
            .ConfigureAwait(false);

        RefreshCache(couponData);
    }

    public async Task RemoveCouponAsync(Guid discountId, string couponCode, CancellationToken ct = default)
    {
        CouponData coupon = await GetCouponAsync(discountId, couponCode, ct)
                .ConfigureAwait(false);

        if (coupon != null)
        {
            await using DbContext db = _databaseFactory.GetDatabase();

            await db.DeleteAsync(coupon, token: ct)
                .ConfigureAwait(false);

            RemoveCache(coupon);
        }
        else
        {
            throw new ArgumentException(nameof(coupon));
        }

    }

    public async Task<CouponData> GetCouponAsync(Guid discountId, string couponCode, CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        CouponData? data = await db.CouponData
            .Where(x => x.DiscountId == discountId && x.CouponCode == couponCode)
            .FirstOrDefaultAsync(token: ct)
            .ConfigureAwait(false);
        return data;
    }

    public async Task<CouponData> GetCouponByKeyAsync(Guid key)
    {
        await using DbContext db = _databaseFactory.GetDatabase();
        CouponData? data = await db.CouponData
            .Where(x => x.CouponKey == key)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        return data;
    }

    public async Task<CouponData> GetCouponByCodeAsync(string couponCode)
    {
        await using DbContext db = _databaseFactory.GetDatabase();
        CouponData? data = await db.CouponData
            .Where(x => x.CouponCode == couponCode)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        return data;
    }

    public async Task<(List<CouponData> Data, int TotalPages)> GetCouponsForDiscountAsync(Guid discountId, string query, int page, int pageSize, CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        int totalCount = await db.CouponData
            .Where(x => x.DiscountId == discountId)
            .Where(x => string.IsNullOrEmpty(query) || x.CouponCode.Contains(query, StringComparison.InvariantCultureIgnoreCase))
            .CountAsync(ct)
            .ConfigureAwait(false);

        // Calculate total pages
        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // Calculate the number of records to skip
        int skip = (page - 1) * pageSize;

        List<CouponData> data = await db.CouponData
            .Where(x => x.DiscountId == discountId)
            .Where(x => string.IsNullOrEmpty(query) || x.CouponCode.Contains(query, StringComparison.InvariantCultureIgnoreCase))
            .OrderByDescending(x => x.Date) // Ensure to order the data before applying Skip and Take
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (data, totalPages);
    }

    public async Task<List<CouponData>> GetCouponsForDiscountAsync(Guid discountId, CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();
        List<CouponData> data = await db.CouponData
            .Where(x => x.DiscountId == discountId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return data;
    }

    public async Task DeleteCouponsByDiscountAsync(Guid discountId, CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();
        List<CouponData> coupons = await GetCouponsForDiscountAsync(discountId, ct).ConfigureAwait(false);

        foreach (CouponData coupon in coupons)
        {
            await db.DeleteAsync(coupon, token: ct).ConfigureAwait(false);

            RemoveCache(coupon);
        }
    }
    public async Task<bool> DiscountHasCouponAsync(Guid discountId, string couponCode)
    {
        await using DbContext db = _databaseFactory.GetDatabase();
        List<CouponData> query = await db.CouponData
            .Where(x => x.DiscountId == discountId && x.CouponCode == couponCode)
            .ToListAsync()
            .ConfigureAwait(false);

        return query.Any();
    }

    public async Task<bool> CouponCodeExistAsync(string couponCode)
    {
        await using DbContext db = _databaseFactory.GetDatabase();
        List<CouponData> query = await db.CouponData
            .Where(x => x.CouponCode == couponCode)
            .ToListAsync()
            .ConfigureAwait(false);

        return query.Any();
    }

    public async Task MarkUsedAsync(string couponCode)
    {
        await using DbContext db = _databaseFactory.GetDatabase();
        CouponData? coupon = await GetCouponByCodeAsync(couponCode)
            .ConfigureAwait(false);

        if (coupon != null)
        {
            coupon.NumberAvailable--;
        }

        await db.UpdateAsync(coupon)
            .ConfigureAwait(false);

        RefreshCache(coupon);
    }

    public void RefreshCache(CouponData coupon)
    {
        _couponCache.AddReplace(coupon);

        RefreshDiscountCache(coupon);
    }

    public void RemoveCache(CouponData coupon)
    {
        _couponCache.Remove(coupon);

        RefreshDiscountCache(coupon);
    }

    public void RefreshDiscountCache(CouponData coupon)
    {
        Cache.ICache? orderDiscountCache = _config.CacheList.Value.FirstOrDefault(x => !string.IsNullOrEmpty(x.NodeAlias) && x.NodeAlias == "ekmOrderDiscount");

        if (orderDiscountCache == null) return;
        // Content service is always null. need to FIX
        UmbracoContent discountNode = _nodeService.NodeById(coupon.DiscountId);

        if (discountNode != null)
        {
            orderDiscountCache.AddReplace(discountNode);
        }
    }
}
