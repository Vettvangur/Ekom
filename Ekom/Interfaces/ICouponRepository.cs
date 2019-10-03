using Ekom.Models.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ekom.Interfaces
{
    public interface ICouponRepository
    {
        Task<bool> CouponCodeExistAsync(string couponCode);
        Task<bool> DiscountHasCouponAsync(Guid discountId, string couponCode);
        Task<CouponData> GetCouponAsync(Guid discountId, string couponCode);
        Task<CouponData> GetCouponByCodeAsync(string couponCode);
        Task<CouponData> GetCouponByKeyAsync(Guid key);
        Task<List<CouponData>> GetCouponsForDiscountAsync(Guid discountId);
        Task InsertCouponAsync(CouponData couponData);
        Task MarkUsedAsync(string couponCode);
        void RefreshCache(CouponData coupon);
        void RefreshDiscountCache(CouponData coupon);
        void RemoveCache(CouponData coupon);
        Task RemoveCouponAsync(Guid discountId, string couponCode);
        Task UpdateCouponAsync(CouponData couponData);
    }
}
