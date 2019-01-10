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

namespace Ekom.Cache
{
    class CouponCache : BaseCache<CouponData>
    { 
        public override string NodeAlias { get; } = "";
        ICouponRepository _couponRepo;

        public CouponCache(
            Configuration config,
            ILogFactory logFac,
            ICouponRepository couponRepo
        ) : base(config, null)
        {
            _couponRepo = couponRepo;
            _log = logFac.GetLogger(typeof(CouponCache));
        }

        public override ConcurrentDictionary<Guid, CouponData> Cache
        {
            get
            {
                if (!_config.PerStoreStock)
                {
                    return base.Cache;
                }

                throw new StockException("PerStoreStock configuration enabled, please disable PerStoreStock before accessing this cache.");
            }
        }

        public override void FillCache()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            _log.Info("Starting to fill...");

            var allCoupons = _couponRepo.GetCoupons();
            foreach (var coupon in allCoupons)
            {
                Cache[coupon.CouponKey] = coupon;
            }

            stopwatch.Stop();
            _log.Info("Finished filling Coupon cache with " + allCoupons.Count() + " items. Time it took to fill: " + stopwatch.Elapsed);
        }
    }
}
