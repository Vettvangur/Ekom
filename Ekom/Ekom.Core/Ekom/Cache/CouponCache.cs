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
    protected IServiceScopeFactory _serviceScopeFactory;

    private readonly Configuration _config;

    public GlobalCouponCache Cache =>
        _config.CacheRebuild.Get(this, nameof(Cache), static () => new GlobalCouponCache());

    public CouponCache(
        ILogger<CouponCache> logger,
        DatabaseFactory databaseFactory, IServiceProvider serviceProvider)
    {
        _config = serviceProvider.GetRequiredService<Configuration>();
        _logger = logger;
        _databaseFactory = databaseFactory;
        _serviceProvider = serviceProvider;
        _serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    /// <inheritdoc />
    public void FillCache()
    {
        using var cacheUpdate = _config.CacheRebuild.Update();
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        _logger.LogInformation("Starting to fill coupon cache...");

        using var scope = _serviceScopeFactory.CreateScope();
        var nodeService = scope.ServiceProvider.GetRequiredService<INodeService>();
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
        using var cacheUpdate = _config.CacheRebuild.Update();
        Cache[coupon.CouponCode.ToLowerInvariant()] = coupon;
    }

    /// <inheritdoc />
    public void Remove(CouponData coupon)
    {
        using var cacheUpdate = _config.CacheRebuild.Update();
        Cache.TryRemove(coupon.CouponCode.ToLowerInvariant(), out _);
    }
}
