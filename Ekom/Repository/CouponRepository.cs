using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Scoping;
using Umbraco.Core.Services;

namespace Ekom.Repository
{
    class CouponRepository : ICouponRepository
    {
        ILogger _logger;
        Configuration _config;
        IAppCache _requestCache;
        IScopeProvider _scopeProvider;
        ICouponCache _couponCache;
        IContentService _contentService;
        /// <summary>
        /// ctor
        /// </summary>
        public CouponRepository(
            Configuration config,
            AppCaches appCaches,
            ILogger logger,
            IScopeProvider scopeProvider,
            ICouponCache couponCache,
            IContentService contentService)
        {
            _config = config;
            _requestCache = appCaches.RequestCache;
            _logger = logger;
            _scopeProvider = scopeProvider;
            _couponCache = couponCache;
            _contentService = contentService;
        }



        public async Task InsertCouponAsync(CouponData couponData)
        {
            if (!await CouponCodeExistAsync(couponData.CouponCode))
            {
                using (var db = _scopeProvider.CreateScope())
                {
                    await db.Database.InsertAsync(couponData)
                        .ConfigureAwait(false);

                    RefreshCache(couponData);
                }
            }
            else
            {
                throw new ArgumentException("Duplicate coupon");
            }

        }

        public async Task UpdateCouponAsync(CouponData couponData)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                await db.UpdateAsync(couponData)
                        .ConfigureAwait(false);

                RefreshCache(couponData);
            }
        }

        public async Task RemoveCouponAsync(Guid discountId, string couponCode)
        {
            var coupon = await GetCouponAsync(discountId, couponCode)
                    .ConfigureAwait(false);

            if (coupon != null)
            {
                using (var db = _scopeProvider.CreateScope().Database)
                {
                    await db.DeleteAsync(coupon)
                        .ConfigureAwait(false);

                    RemoveCache(coupon);
                }
            }
            else
            {
                throw new ArgumentException(nameof(coupon));
            }

        }

        public async Task<CouponData> GetCouponAsync(Guid discountId, string couponCode)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                return await db.FirstOrDefaultAsync<CouponData>("SELECT * FROM EkomCoupon Where DiscountId = @0 AND CouponCode = @1", discountId, couponCode)
                        .ConfigureAwait(false);
            }
        }

        public async Task<CouponData> GetCouponByKeyAsync(Guid key)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                return await db.FirstOrDefaultAsync<CouponData>("SELECT * FROM EkomCoupon Where CouponKey = @0", key)
                        .ConfigureAwait(false);
            }
        }

        public async Task<CouponData> GetCouponByCodeAsync(string couponCode)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                return await db.FirstOrDefaultAsync<CouponData>("SELECT * FROM EkomCoupon Where CouponCode = @0", couponCode)
                        .ConfigureAwait(false);
            }
        }

        public async Task<List<CouponData>> GetAllCouponsAsync()
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                return await db.FetchAsync<CouponData>()
                    .ConfigureAwait(false);
            }
        }

        public async Task<List<CouponData>> GetCouponsForDiscountAsync(Guid discountId)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                return await db.FetchAsync<CouponData>("SELECT * FROM EkomCoupon Where DiscountId = @0", discountId)
                    .ConfigureAwait(false);
            }
        }

        public async Task<bool> DiscountHasCouponAsync(Guid discountId, string couponCode)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                var query = await db.QueryAsync<CouponData>("SELECT * FROM EkomCoupon Where DiscountId = @0 AND CouponCode = @1", discountId, couponCode)
                    .ConfigureAwait(false);

                return query.Any();
            }
        }

        public async Task<bool> CouponCodeExistAsync(string couponCode)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                var query = await db.QueryAsync<CouponData>("SELECT * FROM EkomCoupon Where CouponCode = @0", couponCode)
                        .ConfigureAwait(false);

                return query.Any();
            }
        }

        public async Task MarkUsedAsync(string couponCode)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                var coupon = await GetCouponByCodeAsync(couponCode)
                    .ConfigureAwait(false);

                if (coupon != null)
                {
                    coupon.NumberAvailable--;
                }

                await db.UpdateAsync(coupon)
                    .ConfigureAwait(false);

                RefreshCache(coupon);
            }
        }

        public void RefreshCache(CouponData coupon)
        {
            _couponCache.AddReplace(coupon);

            RefreshDiscountCache(coupon);
        }

        public void RemoveCache(CouponData coupon)
        {
            _couponCache.Remove(coupon);

            RefreshDiscountCache(coupon);
        }

        public void RefreshDiscountCache(CouponData coupon)
        {
            var orderDiscountCache = _config.CacheList.Value.FirstOrDefault(x => !string.IsNullOrEmpty(x.NodeAlias) && x.NodeAlias == "ekmOrderDiscount");

            if (orderDiscountCache != null)
            {
                var cs = _contentService;
                var discountNode = cs.GetById(coupon.DiscountId);

                orderDiscountCache.AddReplace(discountNode);

            }
        }
    }
}
