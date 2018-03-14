using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Discounts;
using Ekom.Models.OrderedObjects;
using System;
using System.Linq;

namespace Ekom.Services
{
    partial class OrderService // : IOrderService
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool ApplyDiscountToOrder(
            IDiscount discount,
            string storeAlias,
            string coupon = null,
            OrderInfo orderInfo = null)
        {
            orderInfo = orderInfo ?? GetOrder(storeAlias);

            if (IsBetterDiscount(orderInfo, discount))
            {
                // Remove worse coupons from orderlines
                foreach (OrderLine line in orderInfo.OrderLines.Where(line => line.Discount != null))
                {
                    if (IsBetterDiscount(line, discount))
                    {
                        line.Discount = null;
                        line.Coupon = null;
                    }
                }

                orderInfo.Discount = new OrderedDiscount(discount);
                orderInfo.Coupon = coupon;

                UpdateOrderAndOrderInfo(orderInfo);

                return true;
            }

            return false;
        }

        public void RemoveDiscountFromOrder(string storeAlias)
        {
            var orderInfo = GetOrder(storeAlias);

            RemoveDiscountFromOrder(orderInfo);
        }
        public void RemoveDiscountFromOrder(OrderInfo orderInfo)
        {
            orderInfo.Discount = null;
            orderInfo.Coupon = null;

            UpdateOrderAndOrderInfo(orderInfo);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <returns></returns>
        public bool ApplyDiscountToOrderLine(
            Guid productKey,
            IDiscount discount,
            string storeAlias,
            string coupon = null,
            OrderInfo orderInfo = null)
        {
            orderInfo = orderInfo ?? GetOrder(storeAlias);
            OrderLine orderLine = orderInfo.OrderLines.FirstOrDefault(line => line.Product.Key == productKey)
                as OrderLine;

            if (orderLine == null)
            {
                throw new OrderLineNotFoundException($"Unable to find order line: {productKey}");
            }

            if (orderLine.Discount != null)
            {
                if (IsBetterDiscount(orderLine, discount))
                {
                    orderLine.Discount = new OrderedDiscount(discount);
                    orderLine.Coupon = coupon;

                    UpdateOrderAndOrderInfo(orderInfo);

                    return true;
                }
            }
            else
            {
                // Apply cart discount on line for comparison with new discount
                orderLine.Discount = orderInfo.Discount;

                if (IsBetterDiscount(orderLine, discount))
                {
                    orderLine.Discount = new OrderedDiscount(discount);
                    orderLine.Coupon = coupon;

                    UpdateOrderAndOrderInfo(orderInfo);

                    return true;
                }

                orderLine.Discount = null;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <returns></returns>
        public void RemoveDiscountFromOrderLine(Guid productKey, string storeAlias)
        {
            var orderInfo = GetOrder(storeAlias);
            var orderLine = orderInfo.OrderLines.FirstOrDefault(line => line.Product.Key == productKey)
                as OrderLine;

            if (orderLine == null)
            {
                throw new OrderLineNotFoundException($"Unable to find order line: {productKey}");
            }

            orderLine.Discount = null;
            orderLine.Coupon = null;

            UpdateOrderAndOrderInfo(orderInfo);
        }

        private bool IsBetterDiscount(OrderInfo orderInfo, IDiscount discount)
        {
            if (orderInfo.Discount == null)
            {
                return true;
            }

            if (orderInfo.Discount.Amount.Type == discount.Amount.Type)
            {
                return discount.CompareTo(orderInfo.Discount) > 0;
            }

            var oldDiscount = orderInfo.Discount;
            var oldTotal = orderInfo.ChargedAmount;

            orderInfo.Discount = new OrderedDiscount(discount);

            var result = orderInfo.ChargedAmount.Value > oldTotal.Value;

            orderInfo.Discount = oldDiscount;

            return result;
        }

        private bool IsBetterDiscount(OrderLine orderLine, IDiscount discount)
        {
            if (orderLine.Discount == null)
            {
                return true;
            }

            if (orderLine.Discount.Amount.Type == discount.Amount.Type)
            {
                return discount.CompareTo(orderLine.Discount) > 0;
            }

            var oldDiscount = orderLine.Discount;
            var oldTotal = orderLine.Amount;

            orderLine.Discount = new OrderedDiscount(discount);

            var result = orderLine.Amount.Value > oldTotal.Value;

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
        /// Verifies all <see cref="Discount"/>'s match their constraints.
        /// Removes non-compliant <see cref="Discount"/>'s
        /// </summary>
        private void VerifyDiscounts(OrderInfo orderInfo)
        {
            if (orderInfo.Discount != null
            && !orderInfo.Discount.Constraints.IsValid(
                orderInfo.StoreInfo.Culture,
                orderInfo.ChargedAmount.Value))
            {
                RemoveDiscountFromOrder(orderInfo.StoreInfo.Alias);
            }
        }
    }
}
