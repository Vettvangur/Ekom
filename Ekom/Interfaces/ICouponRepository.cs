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
        void InsertCoupon(CouponData couponData);
        void UpdateCoupon(CouponData couponData);
        void RemoveCoupon(Guid discountId, string couponCode);
        CouponData GetCoupon(Guid discountId, string couponCode);
        IEnumerable<CouponData> GetAllCoupons();
        bool DiscountHasCoupon(Guid discountId, string couponCode);
        IEnumerable<CouponData> GetCouponsForDiscount(Guid discountId);
        void MarkUsed(string couponCode);
    }}
