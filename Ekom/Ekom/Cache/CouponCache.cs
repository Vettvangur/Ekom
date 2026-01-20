using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using GlobalCouponCache
    = System.Collections.Concurrent.ConcurrentDictionary<string, Ekom.Models.CouponData>;

namespace Ekom.Cache;

/// <inheritdoc />
class CouponCache : ICouponCache
{
    readonly ILogger _logger;
    readonly DatabaseFactory _databaseFactory;
    protected IServiceProvider _serviceProvider;

    protected INodeService nodeService => _serviceProvider.GetService<INodeService>();
    public GlobalCouponCache Cache { get; } = new GlobalCouponCache();

    public CouponCache(
        ILogger<CouponCache> logger,
        DatabaseFactory databaseFactory, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _databaseFactory = databaseFactory;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public void FillCache()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        _logger.LogInformation("Starting to fill coupon cache...");

        List<UmbracoContent> orderDiscountNodes = nodeService.NodesByTypes("ekmOrderDiscount").ToList();

        List<CouponData> allCoupons;
        using (Repositories.DbContext db = _databaseFactory.GetDatabase())
        {
            allCoupons = db.CouponData.ToList();
        }

        foreach (CouponData coupon in allCoupons)
        {
            if (orderDiscountNodes.Any(x => x.Key == coupon.DiscountId))
            {
                Cache[coupon.CouponCode.ToLowerInvariant()] = coupon;
            }
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "Finished filling Coupon cache with {Count} items. Time it took to fill: {Elapsed}",
            allCoupons.Count,
            stopwatch.Elapsed);
    }

    /// <inheritdoc />
    public void AddReplace(CouponData coupon)
    {
        Cache[coupon.CouponCode.ToLowerInvariant()] = coupon;
    }

    /// <inheritdoc />
    public void Remove(CouponData coupon)
    {
        Cache.TryRemove(coupon.CouponCode.ToLowerInvariant(), out _);
    }
}
