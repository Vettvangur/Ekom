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



        public void InsertCoupon(CouponData orderData)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                db.Insert(orderData);
            }
        }

        public void UpdateCoupon(CouponData orderData)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                db.Update(orderData);
            }
        }

        public IEnumerable<CouponData> GetCoupons()
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<CouponData>("");
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
