using Ekom.Interfaces;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Scoping;
using Umbraco.Core.Services;

namespace Ekom.Repository
{
    class CouponRepository : ICouponRepository
    {
        readonly ILogger _logger;
        readonly Configuration _config;
        readonly IAppCache _requestCache;
        readonly IScopeProvider _scopeProvider;
        readonly ICouponCache _couponCache;
        readonly IContentService _contentService;
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
            if (!await CouponCodeExistAsync(couponData.CouponCode)
                .ConfigureAwait(false))
            {
                using (var db = _scopeProvider.CreateScope())
                {
                    await db.Database.InsertAsync(couponData)
                        .ConfigureAwait(false);

                    RefreshCache(couponData);

                    db.Complete();
                }
            }
            else
            {
                throw new ArgumentException("Duplicate coupon");
            }
        }

        public async Task UpdateCouponAsync(CouponData couponData)
        {
            using (var db = _scopeProvider.CreateScope())
            {
                await db.Database.UpdateAsync(couponData)
                        .ConfigureAwait(false);

                RefreshCache(couponData);

                db.Complete();
            }
        }

        public async Task RemoveCouponAsync(Guid discountId, string couponCode)
        {
            var coupon = await GetCouponAsync(discountId, couponCode)
                    .ConfigureAwait(false);

            if (coupon != null)
            {
                using (var db = _scopeProvider.CreateScope())
                {
                    await db.Database.DeleteAsync(coupon)
                        .ConfigureAwait(false);

                    RemoveCache(coupon);

                    db.Complete();
                }
            }
            else
            {
                throw new ArgumentException(nameof(coupon));
            }

        }

        public async Task<CouponData> GetCouponAsync(Guid discountId, string couponCode)
        {
            using (var db = _scopeProvider.CreateScope(autoComplete: true).Database)
            {
                return await db.FirstOrDefaultAsync<CouponData>("SELECT * FROM EkomCoupon Where DiscountId = @0 AND CouponCode = @1", discountId, couponCode)
                        .ConfigureAwait(false);
            }
        }

        public async Task<CouponData> GetCouponByKeyAsync(Guid key)
        {
            using (var db = _scopeProvider.CreateScope(autoComplete: true).Database)
            {
                return await db.FirstOrDefaultAsync<CouponData>("SELECT * FROM EkomCoupon Where CouponKey = @0", key)
                        .ConfigureAwait(false);
            }
        }

        public async Task<CouponData> GetCouponByCodeAsync(string couponCode)
        {
            using (var db = _scopeProvider.CreateScope(autoComplete: true).Database)
            {
                return await db.FirstOrDefaultAsync<CouponData>("SELECT * FROM EkomCoupon Where CouponCode = @0", couponCode)
                        .ConfigureAwait(false);
            }
        }

        public async Task<List<CouponData>> GetCouponsForDiscountAsync(Guid discountId)
        {
            using (var db = _scopeProvider.CreateScope(autoComplete: true).Database)
            {
                return await db.FetchAsync<CouponData>("SELECT * FROM EkomCoupon Where DiscountId = @0", discountId)
                    .ConfigureAwait(false);
            }
        }

        public async Task<bool> DiscountHasCouponAsync(Guid discountId, string couponCode)
        {
            using (var db = _scopeProvider.CreateScope(autoComplete: true).Database)
            {
                var query = await db.QueryAsync<CouponData>("SELECT * FROM EkomCoupon Where DiscountId = @0 AND CouponCode = @1", discountId, couponCode)
                    .ConfigureAwait(false);

                return query.Any();
            }
        }

        public async Task<bool> CouponCodeExistAsync(string couponCode)
        {
            using (var db = _scopeProvider.CreateScope(autoComplete: true).Database)
            {
                var query = await db.QueryAsync<CouponData>("SELECT * FROM EkomCoupon Where CouponCode = @0", couponCode)
                        .ConfigureAwait(false);

                return query.Any();
            }
        }

        public async Task MarkUsedAsync(string couponCode)
        {
            using (var db = _scopeProvider.CreateScope())
            {
                var coupon = await GetCouponByCodeAsync(couponCode)
                    .ConfigureAwait(false);

                if (coupon != null)
                {
                    coupon.NumberAvailable--;
                }

                await db.Database.UpdateAsync(coupon)
                    .ConfigureAwait(false);

                RefreshCache(coupon);

                db.Complete();
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
                // Content service is always null. need to FIX
                var discountNode = _contentService.GetById(coupon.DiscountId);

                if (discountNode != null)
                {
                    orderDiscountCache.AddReplace(discountNode);
                }

            }
        }
    }
}
