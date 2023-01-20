using Ekom.API;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ekom.Services
{
    partial class OrderService
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ApplyDiscountToOrderAsync(
            IDiscount discount,
            string storeAlias = null,
            DiscountOrderSettings settings = null
        )
        {
            if (settings == null)
            {
                settings = new DiscountOrderSettings();
            }

            var orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);

            if (orderInfo.Discount?.Key == discount.Key)
            {
                // throwing an exception allows callers to differentiate between an attempt to apply a worse discount
                // and a duplicate discount application
                // This can then be handled in api controllers or frontend code to display the appropriate error.

                // This was previously inside IsBetterDiscount which is incompatible with automatic global discounts
                throw new DiscountDuplicateException($"Can't add the same discount to order twice.");
            }

            var semaphore = GetOrderLock(orderInfo);
            if (!settings.IsEventHandler)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                if (ApplyDiscountToOrder(discount, orderInfo, settings))
                {
                    if (settings.UpdateOrder)
                    {
                        await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent)
                            .ConfigureAwait(false);
                    }

                    return true;
                }

                return false;
            }
            finally
            {
                if (!settings.IsEventHandler)
                {
                    semaphore.Release();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool ApplyDiscountToOrder(
            IDiscount discount,
            OrderInfo orderInfo,
            DiscountOrderSettings settings
        )
        {
            if (discount is IProductDiscount)
            {
                // This is not correct usage of an IProductDiscount, 
                // they should be automatically applied on OrderLine creation or use
                // ApplyDiscountToOrderLineProductAsync
                throw new NotSupportedException(
                    "Ekom does not currently support comparing or applying ProductDiscounts to OrderInfo, IProductDiscount however inherits from IDiscount for simplicities sake"
                );
            }

            if (IsDiscountApplicable(orderInfo, discount) && IsBetterDiscount(orderInfo, discount))
            {
                // Remove worse coupons from orderlines
                foreach (OrderLine line in orderInfo.OrderLines.Where(line => line.Discount != null))
                {
                    if (!discount.Stackable || IsBetterDiscount(line, discount))
                    {
                        line.Discount = null;
                        line.Coupon = null;
                    }
                }

                orderInfo.Discount = new OrderedDiscount(discount);
                orderInfo.Coupon = settings.Coupon;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Does not remove global discounts currently
        /// </summary>
        public async Task RemoveDiscountFromOrderAsync(string storeAlias, DiscountOrderSettings settings = null)
        {
            if (settings == null)
            {
                settings = new DiscountOrderSettings();
            }

            var orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);

            var semaphore = GetOrderLock(orderInfo);
            if (!settings.IsEventHandler)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                RemoveDiscountFromOrder(orderInfo);
                if (settings.UpdateOrder)
                {
                    await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                if (!settings.IsEventHandler)
                {
                    semaphore.Release();
                }
            }
        }
        private void RemoveDiscountFromOrder(OrderInfo orderInfo)
        {
            orderInfo.Discount = null;
            orderInfo.Coupon = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ProductNotFoundException"></exception>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <returns></returns>
        public async Task<bool> ApplyDiscountToOrderLineProductAsync(
            Guid productKey,
            IDiscount discount,
            string storeAlias,
            DiscountOrderSettings settings = null
        )
        {
            if (settings == null)
            {
                settings = new DiscountOrderSettings();
            }

            IProduct product = Catalog.Instance.GetProduct(productKey);

            if (product == null)
            {
                throw new ProductNotFoundException($"Unable to find product: {productKey}");
            }

            return await ApplyDiscountToOrderLineProductAsync(
                product,
                discount,
                storeAlias,
                settings
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ProductNotFoundException"></exception>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <returns></returns>
        public async Task<bool> ApplyDiscountToOrderLineProductAsync(
            IProduct product,
            IDiscount discount,
            string storeAlias,
            DiscountOrderSettings settings = null
        )
        {
            if (settings == null)
            {
                settings = new DiscountOrderSettings();
            }

            var orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);

            var semaphore = GetOrderLock(orderInfo);
            if (!settings.IsEventHandler)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                var orderLine
                    = orderInfo.OrderLines.FirstOrDefault(line => line.Product.Key == product.Key)
                    as OrderLine;

                if (orderLine == null)
                {
                    throw new OrderLineNotFoundException($"Unable to find order line with product key: {product.Key}");
                }

                return await ApplyDiscountToOrderLineAsync(
                    orderLine,
                    discount,
                    orderInfo,
                    settings
                ).ConfigureAwait(false);
            }
            finally
            {
                if (!settings.IsEventHandler)
                {
                    semaphore.Release();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <returns></returns>
        public async Task<bool> ApplyDiscountToOrderLineAsync(
            Guid lineKey,
            IDiscount discount,
            string storeAlias,
            DiscountOrderSettings settings = null
        )
        {
            if (settings == null)
            {
                settings = new DiscountOrderSettings();
            }

            var orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);

            var semaphore = GetOrderLock(orderInfo);
            if (!settings.IsEventHandler)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                var orderLine
                    = orderInfo.OrderLines.FirstOrDefault(line => line.Key == lineKey)
                    as OrderLine;

                if (orderLine == null)
                {
                    throw new OrderLineNotFoundException($"Unable to find order line: {lineKey}");
                }

                return await ApplyDiscountToOrderLineAsync(
                    orderLine,
                    discount,
                    orderInfo,
                    settings
                ).ConfigureAwait(false);
            }
            finally
            {
                if (!settings.IsEventHandler)
                {
                    semaphore.Release();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <returns></returns>
        private async Task<bool> ApplyDiscountToOrderLineAsync(
            OrderLine orderLine,
            IDiscount discount,
            OrderInfo orderInfo,
            DiscountOrderSettings settings = null
        )
        {
            _logger.LogDebug("Applying discount to orderline");

            if (settings == null)
            {
                settings = new DiscountOrderSettings();
            }

            if (IsDiscountApplicable(orderInfo, orderLine, discount))
            {
                // If a discount is applied to the OrderLine, 
                // assume that discount was better than thecurrent OrderInfo discount. 
                // (We have checks in place that make sure that stays true)
                if (orderLine.Discount != null)
                {
                    if (IsBetterDiscount(orderLine, discount))
                    {
                        orderLine.Discount = new OrderedDiscount(discount);
                        orderLine.Coupon = settings.Coupon;

                        if (settings.UpdateOrder)
                        {
                            await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent)
                                .ConfigureAwait(false);
                        }

                        _logger.LogDebug("Successfully applied discount to orderline");
                        return true;
                    }
                }
                else
                {
                    // Apply cart discount on line for comparison with new discount
                    // was null so we are never overriding
                    orderLine.Discount = orderInfo.Discount;

                    if ((orderInfo.Discount == null || orderInfo.Discount.Stackable)
                    && IsBetterDiscount(orderLine, discount))
                    {
                        orderLine.Discount = new OrderedDiscount(discount);
                        orderLine.Coupon = settings.Coupon;

                        if (settings.UpdateOrder)
                        {
                            await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent)
                                .ConfigureAwait(false);
                        }

                        _logger.LogDebug("Successfully applied discount to orderline");
                        return true;
                    }
                    // When we add a new OrderLine, it might have an applicable ProductDiscount
                    // If the OrderInfo has an exclusive discount we check if the total order price goes down
                    // on applying the ProductDiscount, if so we throw away the OrderInfo discount and use the ProductDiscount instead.
                    else if (orderInfo.Discount?.Stackable == false && IsBetterDiscount(orderInfo, discount))
                    {
                        // It's possible that there exist previous OrderLine's that the ProductDiscount applies to
                        // in that case we assume this new orderline tipped the calculation in favor of this given ProductDiscount
                        // and that the older lines are missing this new about to be applied ProductDiscount (since the OrderInfo one was inclusive)
                        foreach (var line in orderInfo.orderLines)
                        {
                            if (IsDiscountApplicable(orderInfo, line, discount))
                            {
                                line.Discount = new OrderedDiscount(discount);
                            }
                        }

                        orderInfo.Discount = null;
                        _logger.LogDebug("Replaced exclusive OrderInfo discount with a ProductDiscount");
                        return true;
                    }
                    // Only other case is a worse discount

                    orderLine.Discount = null;
                }
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <returns></returns>
        public async Task RemoveDiscountFromOrderLineAsync(
            Guid productKey,
            string storeAlias,
            DiscountOrderSettings settings = null)
        {
            if (settings == null)
            {
                settings = new DiscountOrderSettings();
            }

            var orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);

            var semaphore = GetOrderLock(orderInfo);
            if (!settings.IsEventHandler)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                var orderLine
                    = orderInfo.OrderLines.FirstOrDefault(line => line.Product.Key == productKey)
                    as OrderLine;

                if (orderLine == null)
                {
                    throw new OrderLineNotFoundException($"Unable to find order line: {productKey}");
                }

                RemoveDiscountFromOrderLine(orderLine);

                if (settings.UpdateOrder)
                {
                    await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                if (!settings.IsEventHandler)
                {
                    semaphore.Release();
                }
            }
        }
        private void RemoveDiscountFromOrderLine(OrderLine orderLine)
        {
            if (orderLine == null)
            {
                throw new ArgumentException(nameof(OrderLine));
            }

            orderLine.Discount = null;
            orderLine.Coupon = null;
        }

        private bool IsBetterDiscount(OrderInfo orderInfo, IDiscount discount)
        {
            // Why don't we assume something is better than nothing ?
            // Possibly for orders where all OrderLine have ProductDiscount,
            // in those cases the ChargedAmount will stay the same.
            if (orderInfo.Discount == null && !discount.Stackable && !discount.GlobalDiscount)
            {
                var oldTotal = orderInfo.ChargedAmount.Value;

                orderInfo.Discount = new OrderedDiscount(discount);

                var result = orderInfo.ChargedAmount.Value < oldTotal;

                orderInfo.Discount = null;

                return result;
            }

            if (orderInfo.Discount == null)
            {
                return true;
            }

            if (discount is IProductDiscount productDiscount)
            {
                var oldTotal = orderInfo.ChargedAmount.Value;

                // Save original discounts
                var prevOrderDiscount = orderInfo.Discount;
                var prevDiscounts = new List<OrderedDiscount>();
                foreach (var line in orderInfo.orderLines)
                {
                    prevDiscounts.Add(line.Discount);

                    if (IsDiscountApplicable(orderInfo, line, productDiscount))
                    {
                        line.Discount = new OrderedDiscount(productDiscount);
                    }
                }
                // In case of an exclusive discount, we remove since OrderInfo ChargedAmount ignores
                // product discounts when an exclusive order discount is applied.
                // This ignoring happens for comparison reasons and is explained in ChargedAmount.
                orderInfo.Discount = null;
                // Compare
                var result = orderInfo.ChargedAmount.Value < oldTotal;

                // Reset to previous discounts
                orderInfo.Discount = prevOrderDiscount;
                for (var x = 0; x < orderInfo.OrderLines.Count; x++)
                {
                    orderInfo.orderLines[x].Discount = prevDiscounts.ElementAt(x);
                }

                return result;
            }
            else
            {
                // In case of comparing an Exclusive to an inclusive discount, this simple CompareTo
                // does not apply
                //if (orderInfo.Discount.Type == discount.Type)
                //{
                //    return discount.CompareTo(orderInfo.Discount) > 0;
                //}

                var oldDiscount = orderInfo.Discount;
                var oldTotal = orderInfo.ChargedAmount.Value;

                orderInfo.Discount = new OrderedDiscount(discount);

                var result = orderInfo.ChargedAmount.Value < oldTotal;

                orderInfo.Discount = oldDiscount;

                return result;
            }
        }

        private bool IsBetterDiscount(OrderLine orderLine, IDiscount discount)
        {
            if (orderLine.Discount == null)
            {
                return true;
            }

            // This shouldn't really hit, we are probably checking for stackable before and
            // it's hard to see global discounts supporting stackable.
            if (discount.GlobalDiscount && IsDiscountApplicable(orderLine.OrderInfo, orderLine, discount))
            {
                return false;
            }

            if (orderLine.Discount.Type == discount.Type)
            {
                return discount.CompareTo(orderLine.Discount) > 0;
            }

            var oldDiscount = orderLine.Discount;
            var oldTotal = orderLine.Amount;

            orderLine.Discount = new OrderedDiscount(discount);

            var result = orderLine.Amount.Value < oldTotal.Value;

            orderLine.Discount = oldDiscount;

            return result;
        }

        /// <summary>
        /// Although Discounts are store specific, coupons are not.
        /// We therefore 
        /// </summary>
        /// <param name="Key"></param>
        public void CouponApply(Guid Key)
        {
            var defStore = _storeSvc.GetAllStores().First();
            var discount = _discountCache[defStore.Alias][Key];

            (discount as Discount)?.OnCouponApply();
        }

        /// <summary>
        /// Finds Global discounts that apply to order, 
        /// checks constraints and applies automatically if applicable.
        /// </summary>
        /// <param name="orderInfo"></param>
        /// <returns></returns>
        private void AddGlobalDiscounts(OrderInfo orderInfo)
        {
            var discounts = Discounts.Instance.GetGlobalDiscounts(orderInfo.StoreInfo.Alias);
            foreach (var discount in discounts)
            {
                ApplyDiscountToOrder(
                    discount,
                    orderInfo,
                    new DiscountOrderSettings
                    {
                        //UpdateOrder = false, // not technically needed for this method
                    });
            }
        }

        /// <summary>
        /// Verifies all <see cref="Discount"/>'s match their constraints.
        /// Removes non-compliant <see cref="Discount"/>'s
        /// 
        /// Gets called on OrderInfo updates, constraints may become invalid if the order total changes.
        /// </summary>
        private void VerifyDiscounts(OrderInfo orderInfo)
        {
            var total = orderInfo.OrderLineTotal.Value;
            var storeAlias = orderInfo.StoreInfo.Alias;

            // Verify order discount constraints
            if (orderInfo.Discount != null
            && !orderInfo.Discount.Constraints.IsValid(
                storeAlias,
                total))
            {
                RemoveDiscountFromOrder(orderInfo);
            }

            //var curStoreDiscCache = _discountCache.GlobalDiscounts[storeAlias];

            //var gds = curStoreDiscCache
            //    .Where(gd => gd.Value.Constraints.IsValid(storeAlias, total))
            //    .Select(gd => gd.Value)
            //    .ToList();

            //// Try apply global order discounts
            //foreach (var gd in gds)
            //{
            //    //ApplyDiscountToOrder(gd, orderInfo, coupon: null);
            //}

            // Verify order line discount constraints
            foreach (var line in orderInfo.orderLines)
            {
                if (line.Discount != null)
                {
                    if (line.Discount?.Constraints.IsValid(storeAlias, total) == false
                    || !IsDiscountApplicable(orderInfo, line, line.Discount))
                    {
                        RemoveDiscountFromOrderLine(line);
                    }
                }
            }
        }

        /// <summary>
        /// Do constraints hold for the given discount
        /// </summary>
        /// <param name="orderInfo"></param>
        /// <param name="discount"></param>
        /// <returns></returns>
        private bool IsDiscountApplicable(IOrderInfo orderInfo, IDiscount discount)
            => discount.Constraints.IsValid(orderInfo.StoreInfo.Culture, orderInfo.OrderLineTotal.Value);

        /// <summary>
        /// Do constraints hold and do discount items match if any
        /// </summary>
        /// <param name="orderInfo"></param>
        /// <param name="orderLine"></param>
        /// <param name="discount"></param>
        /// <returns></returns>
        public static bool IsDiscountApplicable(IOrderInfo orderInfo, IOrderLine orderLine, IDiscount discount)
        {
            return discount.Constraints.IsValid(orderInfo.StoreInfo.Culture, orderInfo.OrderLineTotal.Value)
                && (discount.DiscountItems.Count == 0
                || (orderLine.Product.Path.Split(',').Intersect(discount.DiscountItems).Any())
                || (orderLine.Product.Properties.GetPropertyValue("categories").Split(',').Select(x => Configuration.Resolver.GetService<INodeService>().NodeById(x)?.Id.ToString()).Intersect(discount.DiscountItems).Any())
                );
        }

        public async Task InsertCouponCodeAsync(string couponCode, int numberAvailable, Guid discountId)
        {
            if (string.IsNullOrEmpty(couponCode))
            {
                throw new ArgumentException("string.IsNullOrEmpty", nameof(couponCode));
            }

            if (discountId == Guid.Empty)
            {
                throw new ArgumentException("string.IsNullOrEmpty", nameof(discountId));
            }

            await _couponRepository.InsertCouponAsync(new CouponData()
            {
                CouponCode = couponCode.ToLowerInvariant(),
                CouponKey = Guid.NewGuid(),
                DiscountId = discountId,
                NumberAvailable = numberAvailable
            }).ConfigureAwait(false);
        }

        public async Task RemoveCouponCodeAsync(string couponCode, Guid discountId)
        {
            if (string.IsNullOrEmpty(couponCode))
            {
                throw new ArgumentException("string.IsNullOrEmpty", nameof(couponCode));
            }

            if (discountId == Guid.Empty)
            {
                throw new ArgumentException("== Guid.Empty", nameof(discountId));
            }

            await _couponRepository.RemoveCouponAsync(discountId, couponCode)
                .ConfigureAwait(false);
        }

        public async Task<List<CouponData>> GetCouponsForDiscountAsync(Guid discountId)
        {
            if (discountId == Guid.Empty)
            {
                throw new ArgumentException("== Guid.Empty", nameof(discountId));
            }

            return await _couponRepository.GetCouponsForDiscountAsync(discountId)
                .ConfigureAwait(false);
        }
    }
}
