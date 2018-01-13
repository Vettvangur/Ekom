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
        /// <returns></returns>
        public bool ApplyDiscountToOrder(Discount discount, string coupon, string storeAlias)
        {
            var orderInfo = GetOrder(storeAlias);

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

                orderInfo.Discount = discount;
                orderInfo.Coupon = coupon;

                UpdateOrderAndOrderInfo(orderInfo);

                return true;
            }

            return false;
        }

        public void RemoveDiscountFromOrder(string storeAlias)
        {
            var orderInfo = GetOrder(storeAlias);

            orderInfo.Discount = null;
            orderInfo.Coupon = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <returns></returns>
        public bool ApplyDiscountToOrderLine(Guid productKey, Discount discount, string coupon, string storeAlias)
        {
            var orderInfo = GetOrder(storeAlias);
            OrderLine orderLine = orderInfo.OrderLines.FirstOrDefault(line => line.Id == productKey)
                as OrderLine;

            if (orderLine == null)
            {
                throw new OrderLineNotFoundException($"Unable to find order line: {productKey}");
            }

            if (orderLine.Discount != null)
            {
                if (IsBetterDiscount(orderLine, discount))
                {
                    orderLine.Discount = discount;
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
                    orderLine.Discount = discount;
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
            var orderLine = orderInfo.OrderLines.FirstOrDefault(line => line.Id == productKey)
                as OrderLine;

            if (orderLine == null)
            {
                throw new OrderLineNotFoundException($"Unable to find order line: {productKey}");
            }

            orderLine.Discount = null;
            orderLine.Coupon = null;

            UpdateOrderAndOrderInfo(orderInfo);
        }

        private bool IsBetterDiscount(OrderInfo orderInfo, Discount discount)
        {
            if (orderInfo.Discount.Amount.Type
            == discount.Amount.Type)
                return discount > orderInfo.Discount;

            var oldDiscount = orderInfo.Discount;
            var oldTotal = orderInfo.ChargedAmount;

            orderInfo.Discount = discount;

            var result = orderInfo.ChargedAmount.Value > oldTotal.Value;

            orderInfo.Discount = oldDiscount;

            return result;
        }

        private bool IsBetterDiscount(OrderLine orderLine, Discount discount)
        {
            if (orderLine.Discount.Amount.Type
            == discount.Amount.Type)
                return discount > orderLine.Discount;

            var oldDiscount = orderLine.Discount;
            var oldTotal = orderLine.Amount;

            orderLine.Discount = discount;

            var result = orderLine.Amount.Value > oldTotal.Value;

            orderLine.Discount = oldDiscount;

            return result;
        }
    }
}
