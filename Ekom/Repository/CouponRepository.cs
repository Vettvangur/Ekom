using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            if (!DiscountHasCoupon(couponData.DiscountId,couponData.CouponCode))
            {
                using (var db = _appCtx.DatabaseContext.Database)
                {
                    db.Insert(couponData);
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

        public void MarkUsed(Guid CouponKey)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                db.Update("update DBO.EkomCoupon c set c.NumberAvailable = c.NumberAvailable -1 where c.CouponKey = @0", CouponKey);
            }
        }
    }
}
