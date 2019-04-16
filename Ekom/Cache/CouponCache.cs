using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using GlobalCouponCache
    = System.Collections.Concurrent.ConcurrentDictionary<string, Ekom.Models.Data.CouponData>;

namespace Ekom.Cache
{
    public class CouponCache : ICache
    {
        public string NodeAlias { get; } = "couponCache";
        protected ILog _log;
        ICouponRepository _couponRepo;
        public ConcurrentDictionary<string, CouponData> Cache;
        public CouponCache(
            ILogFactory logFac,
            ICouponRepository couponRepo
        ) 
        {
            _couponRepo = couponRepo;
            _log = logFac.GetLogger(typeof(CouponCache));

        }
       
        public void FillCache()
        {
            Cache = new GlobalCouponCache();
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            _log.Info("Starting to fill coupon cache...");

            var allCoupons = _couponRepo.GetAllCoupons();

            foreach (var coupon in allCoupons)
            {
                Cache[coupon.CouponCode.ToLowerInvariant()] = coupon;
            }

            stopwatch.Stop();
            _log.Info("Finished filling Coupon cache with " + allCoupons.Count() + " items. Time it took to fill: " + stopwatch.Elapsed);
        }

        public void AddReplace(CouponData coupon)
        {
            Cache[coupon.CouponCode.ToLowerInvariant()] = coupon;
        }

        public void AddReplace(IContent node)
        {

        }

        public void Remove(Guid key)
        {
        }

        public void Remove(CouponData coupon)
        {
            CouponData i = null;

            Cache.TryRemove(coupon.CouponCode.ToLowerInvariant(), out i);
        }

    }
}
