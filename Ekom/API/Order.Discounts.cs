using Ekom.Exceptions;
using Ekom.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ekom.API
{
    /// <summary>
    /// The Ekom API, get/update/remove operations on orders 
    /// </summary>
    public partial class Order
    {
        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public async Task<bool> ApplyCouponToOrderAsync(string coupon)
        {
            var storeAlias = _storeSvc.GetStoreFromCache().Alias;

            return await ApplyCouponToOrderAsync(coupon, storeAlias)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public async Task<bool> ApplyCouponToOrderAsync(string coupon, string storeAlias)
        {
            if (string.IsNullOrEmpty(coupon))
            {
                throw new ArgumentException("string.IsNullOrEmpty", nameof(coupon));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("string.IsNullOrEmpty", nameof(storeAlias));
            }

            coupon = coupon.ToLowerInvariant();

            if (_couponCache.Cache.TryGetValue(coupon, out var couponData))
            {
                if (couponData.NumberAvailable > 0)
                {
                    if (_discountCache.Cache[storeAlias].TryGetValue(couponData.DiscountId, out var discount))
                    {
                        return await _orderService.ApplyDiscountToOrderAsync(
                            discount, 
                            storeAlias, 
                            new DiscountOrderSettings
                            {
                                Coupon = coupon,
                            })
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        throw new DiscountNotFoundException($"Unable to find discount with coupon {coupon}");
                    }
                }
                else
                {
                    throw new DiscountNotFoundException($"Coupon has no usage.");
                }

            }

            else
            {
                throw new DiscountNotFoundException($"Unable to find couponCode {coupon}");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public async Task RemoveCouponFromOrderAsync()
        {
            var storeAlias = _storeSvc.GetStoreFromCache().Alias;

            await RemoveCouponFromOrderAsync(storeAlias).ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="storeAlias"></param>
        /// <exception cref="ArgumentException"></exception>
        public async Task RemoveCouponFromOrderAsync(string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("string.IsNullOrEmpty", nameof(storeAlias));
            }

            await _orderService.RemoveDiscountFromOrderAsync(storeAlias)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public async Task<bool> ApplyCouponToOrderLineAsync(Guid productKey, string coupon)
        {
            var storeAlias = _storeSvc.GetStoreFromCache().Alias;

            return await ApplyCouponToOrderLineAsync(productKey, coupon, storeAlias)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public async Task<bool> ApplyCouponToOrderLineAsync(Guid productKey, string coupon, string storeAlias)
        {
            if (string.IsNullOrEmpty(coupon))
            {
                throw new ArgumentException("string.IsNullOrEmpty", nameof(coupon));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("string.IsNullOrEmpty", nameof(storeAlias));
            }
            if (productKey == Guid.Empty)
            {
                throw new ArgumentException("== Guid.Empty", nameof(productKey));
            }

            coupon = coupon.ToLowerInvariant();

            if (_couponCache.Cache.TryGetValue(coupon, out var couponData))
            {
                if (_discountCache.Cache[storeAlias].TryGetValue(couponData.DiscountId, out var discount))
                {
                    return await _orderService.ApplyDiscountToOrderLineAsync(
                        productKey,
                        discount,
                        storeAlias,
                        new DiscountOrderSettings
                        {
                            Coupon = coupon,
                        })
                        .ConfigureAwait(false);
                }
                else
                {
                    throw new DiscountNotFoundException($"Unable to find discount with coupon {coupon}");
                }

            }
            else
            {
                throw new DiscountNotFoundException($"Unable to find discount with coupon {coupon}");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OrderLineNotFoundException"></exception>
        public async Task RemoveCouponFromOrderLineAsync(Guid productKey)
        {
            var storeAlias = _storeSvc.GetStoreFromCache().Alias;

            await RemoveCouponFromOrderLineAsync(productKey, storeAlias)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="productKey"></param>
        /// <param name="storeAlias"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OrderLineNotFoundException"></exception>
        public async Task RemoveCouponFromOrderLineAsync(Guid productKey, string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException("string.IsNullOrEmpty", nameof(storeAlias));
            }
            if (productKey == Guid.Empty)
            {
                throw new ArgumentException("== Guid.Empty", nameof(productKey));
            }

            await _orderService.RemoveDiscountFromOrderLineAsync(productKey, storeAlias)
                .ConfigureAwait(false);
        }

        public async Task InsertCouponCodeAsync(string couponCode, int numberAvailable, Guid discountId)
        {
            await _orderService.InsertCouponCodeAsync(couponCode, numberAvailable, discountId)
                .ConfigureAwait(false);
        }

        public async Task RemoveCouponCodeAsync(string couponCode, Guid discountId)
        {
            await _orderService.RemoveCouponCodeAsync(couponCode, discountId)
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<CouponData>> GetCouponsForDiscountAsync(Guid discountId)
        {
            return await _orderService.GetCouponsForDiscountAsync(discountId)
                .ConfigureAwait(false);
        }
    }
}
