using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;

namespace Ekom.Repository
{
    public class CouponRepository : ICouponRepository
    {
        ILog _log;
        Configuration _config;
        ApplicationContext _appCtx;
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="config"></param>
        /// <param name="appCtx "></param>
        /// <param name="logFac"></param>
        public CouponRepository(Configuration config, ApplicationContext appCtx, ILogFactory logFac)
        {
            _config = config;
            _appCtx = appCtx;
            _log = logFac.GetLogger<CouponRepository>();
        }



        public void InsertCoupon(CouponData couponData)
        {
            if (!CouponCodeExist(couponData.CouponCode))
            {
                using (var db = _appCtx.DatabaseContext.Database)
                {
                    db.Insert(couponData);

                    RefreshCache(couponData);
                }
            } else
            {
                throw new ArgumentException("Duplicate coupon");
            }

        }

        public void UpdateCoupon(CouponData couponData)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                db.Update(couponData);

                RefreshCache(couponData);
            }
        }

        public void RemoveCoupon(Guid discountId, string couponCode)
        {
            var coupon = GetCoupon(discountId, couponCode);

            if (coupon != null)
            {
                using (var db = _appCtx.DatabaseContext.Database)
                {
                    db.Delete(coupon);

                    RemoveCache(coupon);
                }
            } else
            {
                throw new ArgumentException(nameof(coupon));
            }

        }

        public CouponData GetCoupon(Guid discountId, string couponCode)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.FirstOrDefault<CouponData>("SELECT * FROM EkomCoupon Where DiscountId = @0 AND CouponCode = @1", discountId, couponCode);
            }
        }

        public CouponData GetCouponByKey(Guid key)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.FirstOrDefault<CouponData>("SELECT * FROM EkomCoupon Where CouponKey = @0", key);
            }
        }

        public CouponData GetCouponByCode(string couponCode)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.FirstOrDefault<CouponData>("SELECT * FROM EkomCoupon Where CouponCode = @0", couponCode);
            }
        }

        public IEnumerable<CouponData> GetAllCoupons()
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<CouponData>("");
            }
        }

        public IEnumerable<CouponData> GetCouponsForDiscount(Guid discountId)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<CouponData>("SELECT * FROM EkomCoupon Where DiscountId = @0", discountId);
            }
        }

        public bool DiscountHasCoupon(Guid discountId, string couponCode)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<CouponData>("SELECT * FROM EkomCoupon Where DiscountId = @0 AND CouponCode = @1", discountId, couponCode).Any();
            }
        }

        public bool CouponCodeExist(string couponCode)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<CouponData>("SELECT * FROM EkomCoupon Where CouponCode = @0", couponCode).Any();
            }
        }

        public void MarkUsed(string couponCode)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                var coupon = GetCouponByCode(couponCode);

                if (coupon != null)
                {
                    coupon.NumberAvailable--;
                }

                db.Update(coupon);

                RefreshCache(coupon);
            }

        }

        public void RefreshCache(CouponData coupon)
        {
            var couponCache = _config.CacheList.Value.FirstOrDefault(x => !string.IsNullOrEmpty(x.NodeAlias) && x.NodeAlias == "couponCache");

            if (couponCache != null)
            {
                couponCache.AddReplace(coupon);
            }

            RefreshDiscountCache(coupon);
        }

        public void RemoveCache(CouponData coupon)
        {
            var couponCache = _config.CacheList.Value.FirstOrDefault(x => !string.IsNullOrEmpty(x.NodeAlias) && x.NodeAlias == "couponCache");

            if (couponCache != null)
            {
                couponCache.Remove(coupon);
            }

            RefreshDiscountCache(coupon);
        }

        public void RefreshDiscountCache(CouponData coupon)
        {
            var orderDiscountCache = _config.CacheList.Value.FirstOrDefault(x => !string.IsNullOrEmpty(x.NodeAlias) && x.NodeAlias == "ekmOrderDiscount");

            if (orderDiscountCache != null)
            {
                var cs = ApplicationContext.Current.Services.ContentService;
                var discountNode = cs.GetById(coupon.DiscountId);

                orderDiscountCache.AddReplace(discountNode);
            }
        }
    }
}
