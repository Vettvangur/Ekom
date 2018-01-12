using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Discounts;
using System;
using System.Linq;

namespace Ekom.Services
{
    partial class OrderService // : IOrderService
    {
        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public bool ApplyDiscountToOrder(Guid discountKey, string storeAlias)
        {
            if (discountKey == Guid.Empty)
            {
                throw new ArgumentException(nameof(discountKey));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            Discount discount = _discountCache[storeAlias][discountKey];

            if (discount == null)
            {
                throw new DiscountNotFoundException($"Unable to find discount: {discountKey}");
            }

            var orderInfo = GetOrder(storeAlias);

            if (IsBetterDiscount(orderInfo, discount))
            {
                if (orderInfo.discount != null)
                {
                    orderInfo.discount.OnCouponRemove();
                }

                // Remove worse coupons from orderlines
                foreach (var line in orderInfo.OrderLines.Where(line => line.discount != null))
                {
                    if (IsBetterDiscount(line, discount))
                    {
                        line.discount.OnCouponRemove();
                        line.discount = null;
                    }
                }

                discount.OnCouponApply();
                orderInfo.discount = discount;

                UpdateOrderAndOrderInfo(orderInfo);

                return true;
            }

            return false;
        }

        public void RemoveDiscountFromOrder(string storeAlias)
        {
            var orderInfo = GetOrder(storeAlias);

            orderInfo.discount?.OnCouponRemove();
            orderInfo.discount = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public bool ApplyDiscountToOrderLine(Guid productKey, Guid discountKey, string storeAlias)
        {
            if (productKey == Guid.Empty)
            {
                throw new ArgumentException(nameof(productKey));
            }
            if (discountKey == Guid.Empty)
            {
                throw new ArgumentException(nameof(discountKey));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            Discount discount = _discountCache[storeAlias][discountKey];

            if (discount == null)
            {
                throw new DiscountNotFoundException($"Unable to find discount: {discountKey}");
            }

            var orderInfo = GetOrder(storeAlias);
            var orderLine = orderInfo.OrderLines.FirstOrDefault(line => line.Id == productKey);

            if (orderLine == null)
            {
                throw new OrderLineNotFoundException($"Unable to find order line: {productKey}");
            }

            if (orderLine.discount != null)
            {
                if (IsBetterDiscount(orderLine, discount))
                {
                    orderLine.discount?.OnCouponRemove();

                    discount.OnCouponApply();
                    orderLine.discount = discount;

                    UpdateOrderAndOrderInfo(orderInfo);

                    return true;
                }
            }
            else
            {
                // Apply cart discount on line for comparison with new discount
                orderLine.discount = orderInfo.discount;

                if (IsBetterDiscount(orderLine, discount))
                {
                    discount.OnCouponApply();
                    orderLine.discount = discount;

                    UpdateOrderAndOrderInfo(orderInfo);

                    return true;
                }

                orderLine.discount = null;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public void RemoveDiscountFromOrderLine(Guid productKey, string storeAlias)
        {
            if (productKey == Guid.Empty)
            {
                throw new ArgumentException(nameof(productKey));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            var orderInfo = GetOrder(storeAlias);
            var orderLine = orderInfo.OrderLines.FirstOrDefault(line => line.Id == productKey);

            if (orderLine != null)
            {
                orderLine.discount?.OnCouponRemove();
                orderLine.discount = null;

                UpdateOrderAndOrderInfo(orderInfo);
                return;
            }

            throw new OrderLineNotFoundException($"Unable to find order line: {productKey}");
        }

        private bool IsBetterDiscount(OrderInfo orderInfo, Discount discount)
        {
            if (orderInfo.discount.Amount.Type
            == discount.Amount.Type)
                return discount > orderInfo.discount;

            var oldDiscount = orderInfo.discount;
            var oldTotal = orderInfo.ChargedAmount;

            orderInfo.discount = discount;

            var result = orderInfo.ChargedAmount.Value > oldTotal.Value;

            orderInfo.discount = oldDiscount;

            return result;
        }

        private bool IsBetterDiscount(OrderLine orderLine, Discount discount)
        {
            if (orderLine.discount.Amount.Type
            == discount.Amount.Type)
                return discount > orderLine.discount;

            var oldDiscount = orderLine.discount;
            var oldTotal = orderLine.Amount;

            orderLine.discount = discount;

            var result = orderLine.Amount.Value > oldTotal.Value;

            orderLine.discount = oldDiscount;

            return result;
        }
    }
}
