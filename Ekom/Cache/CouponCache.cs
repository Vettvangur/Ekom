using Ekom.Interfaces;
using Ekom.Models.Data;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Scoping;
using GlobalCouponCache
    = System.Collections.Concurrent.ConcurrentDictionary<string, Ekom.Models.Data.CouponData>;

namespace Ekom.Cache
{
    /// <inheritdoc />
    class CouponCache : ICouponCache
    {
        readonly ILogger _logger;
        readonly IScopeProvider _scopeProvider;
        readonly IUmbracoDatabaseFactory _databaseFactory;

        public GlobalCouponCache Cache { get; } = new GlobalCouponCache();

        public CouponCache(
            ILogger logger,
            IScopeProvider scopeProvider,
            IUmbracoDatabaseFactory databaseFactory
        )
        {
            _scopeProvider = scopeProvider;
            _logger = logger;
            _databaseFactory = databaseFactory;
        }

        /// <inheritdoc />
        public void FillCache()
        {
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            _logger.Info<CouponCache>("Starting to fill coupon cache...");

            List<CouponData> allCoupons;
            using (var db = _databaseFactory.CreateDatabase())
            {
                //var db = scope.Database;
                allCoupons = db.FetchAsync<CouponData>().Result;
                //scope.Complete();
            }

            foreach (var coupon in allCoupons)
            {
                Cache[coupon.CouponCode.ToLowerInvariant()] = coupon;
            }

#if DEBUG
            stopwatch.Stop();
            _logger.Info<CouponCache>(
                "Finished filling Coupon cache with {Count} items. Time it took to fill: {Elapsed}",
                allCoupons.Count,
                stopwatch.Elapsed);
#else
            _logger.Debug<CouponCache>(
                "Finished filling Coupon cache with {Count} items.",
                allCoupons.Count);
#endif
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
}
