using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using GlobalCouponCache
    = System.Collections.Concurrent.ConcurrentDictionary<string, Ekom.Models.Data.CouponData>;

namespace Ekom.Cache
{
    /// <inheritdoc />
    class CouponCache : ICouponCache
    {
        readonly ILogger _logger;
        readonly ICouponRepository _couponRepo;

        public ConcurrentDictionary<string, CouponData> Cache
            = new GlobalCouponCache();

        public CouponCache(
            ILogger logger,
            ICouponRepository couponRepo
        )
        {
            _couponRepo = couponRepo;
            _logger = logger;
        }

        /// <inheritdoc />
        public void FillCache()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            _logger.Info<CouponCache>("Starting to fill coupon cache...");

            var allCoupons = _couponRepo.GetAllCouponsAsync().Result;

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
