using Ekom.Interfaces;
using Ekom.Models.Data;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Umbraco.Core.Logging;
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

        public ConcurrentDictionary<string, CouponData> Cache { get; }
            = new GlobalCouponCache();

        public CouponCache(
            ILogger logger,
            IScopeProvider scopeProvider
        )
        {
            _scopeProvider = scopeProvider;
            _logger = logger;
        }

        /// <inheritdoc />
        public void FillCache()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            _logger.Info<CouponCache>("Starting to fill coupon cache...");

            List<CouponData> allCoupons;
            using (var db = _scopeProvider.CreateScope().Database)
            {
                allCoupons = db.FetchAsync<CouponData>().Result;
            }

            foreach (var coupon in allCoupons)
            {
                Cache[coupon.CouponCode.ToLowerInvariant()] = coupon;
            }

            stopwatch.Stop();
            _logger.Info<CouponCache>("Finished filling Coupon cache with " + allCoupons.Count() + " items. Time it took to fill: " + stopwatch.Elapsed);
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
