using Ekom.Exceptions;
using System;

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
        public bool ApplyCouponToOrder(string coupon)
        {
            var storeAlias = _storeSvc.GetStoreFromCache().Alias;

            return ApplyCouponToOrder(coupon, storeAlias);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public bool ApplyCouponToOrder(string coupon, string storeAlias)
        {
            if (string.IsNullOrEmpty(coupon))
            {
                throw new ArgumentException(nameof(coupon));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }
           
            if (_couponCache.Cache.TryGetValue(coupon, out var couponData))
            {
                if (_discountCache.GlobalDiscounts[storeAlias].TryGetValue(couponData.DiscountId, out var discount))
                { 
                    return _orderService.ApplyDiscountToOrder(discount, storeAlias, coupon);
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
        public void RemoveCouponFromOrder()
        {
            var storeAlias = _storeSvc.GetStoreFromCache().Alias;

            RemoveCouponFromOrder(storeAlias);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="storeAlias"></param>
        /// <exception cref="ArgumentException"></exception>
        public void RemoveCouponFromOrder(string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            _orderService.RemoveDiscountFromOrder(storeAlias);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public bool ApplyCouponToOrderLine(Guid productKey, string coupon)
        {
            var storeAlias = _storeSvc.GetStoreFromCache().Alias;

            return ApplyCouponToOrderLine(productKey, coupon, storeAlias);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public bool ApplyCouponToOrderLine(Guid productKey, string coupon, string storeAlias)
        {
            if (string.IsNullOrEmpty(coupon))
            {
                throw new ArgumentException(nameof(coupon));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }
            if (productKey == Guid.Empty)
            {
                throw new ArgumentException(nameof(productKey));
            }
           
            if (_couponCache.Cache.TryGetValue(coupon, out var couponData))
            {
                if (_discountCache.GlobalDiscounts[storeAlias].TryGetValue(couponData.DiscountId, out var discount))
                {
                    return _orderService.ApplyDiscountToOrderLine(productKey, discount, storeAlias, coupon);
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
        public void RemoveCouponFromOrderLine(Guid productKey)
        {
            var storeAlias = _storeSvc.GetStoreFromCache().Alias;

            RemoveCouponFromOrderLine(productKey, storeAlias);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="productKey"></param>
        /// <param name="storeAlias"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OrderLineNotFoundException"></exception>
        public void RemoveCouponFromOrderLine(Guid productKey, string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }
            if (productKey == Guid.Empty)
            {
                throw new ArgumentException(nameof(productKey));
            }

            _orderService.RemoveDiscountFromOrderLine(productKey, storeAlias);
        }
    }
}
