using Ekom.Models.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Interfaces
{
    public interface ICouponRepository
    {
        void InsertCoupon(CouponData orderData);
        void UpdateCoupon(CouponData orderData);
        IEnumerable<CouponData> GetCoupons();
    }
}
