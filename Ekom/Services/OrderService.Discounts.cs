using Ekom.API;
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
            string storeAlias = null,
            string coupon = null,
            OrderInfo orderInfo = null
        )
        {
            orderInfo = orderInfo ?? GetOrder(storeAlias);

            if (ApplyDiscountToOrder(discount, orderInfo, coupon))
            {
                UpdateOrderAndOrderInfo(orderInfo);
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool ApplyDiscountToOrder(
            IDiscount discount,
            OrderInfo orderInfo,
            string coupon = null
        )
        {
            if (IsBetterDiscount(orderInfo, discount))
            {
                // Remove worse coupons from orderlines
                //foreach (OrderLine line in orderInfo.OrderLines.Where(line => line.Discount != null))
                //{
                //    if (IsBetterDiscount(line, discount))
                //    {
                //        line.Discount = null;
                //        line.Coupon = null;
                //    }
                //}

                orderInfo.Discount = new OrderedDiscount(discount);
                orderInfo.Coupon = coupon;
                foreach(OrderLine line in orderInfo.OrderLines.Where(line => line.Discount == null))
                {
                    if (line.Discount == null)
                    {
                        if (discount.DiscountItems.Contains(line.ProductKey))
                        {
                            line.Discount = orderInfo.Discount;
                            line.Coupon = coupon;
                        }
                    }
                }
                

                

                return true;
            }

            return false;
        }

        public void RemoveDiscountFromOrder(string storeAlias)
        {
            var orderInfo = GetOrder(storeAlias);

            RemoveDiscountFromOrder(orderInfo);
            UpdateOrderAndOrderInfo(orderInfo);
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
        public bool ApplyDiscountToOrderLineProduct(
            Guid productKey,
            IDiscount discount,
            string storeAlias = null,
            string coupon = null,
            OrderInfo orderInfo = null
        )
        {
            IProduct product = Catalog.Instance.GetProduct(productKey);

            if (product == null)
            {
                throw new ProductNotFoundException($"Unable to find product: {productKey}");
            }

            return ApplyDiscountToOrderLineProduct(
                product,
                discount,
                storeAlias,
                coupon,
                orderInfo
            );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ProductNotFoundException"></exception>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <returns></returns>
        public bool ApplyDiscountToOrderLineProduct(
            IProduct product,
            IDiscount discount,
            string storeAlias = null,
            string coupon = null,
            OrderInfo orderInfo = null
        )
        {

            orderInfo = orderInfo ?? GetOrder(storeAlias);
            OrderLine orderLine
                = orderInfo.OrderLines.FirstOrDefault(line => line.Product.Key == product.Key)
                as OrderLine;

            if (orderLine == null)
            {
                throw new OrderLineNotFoundException($"Unable to find order line with product key: {product.Key}");
            }

            return ApplyDiscountToOrderLine(
                orderLine,
                discount,
                orderInfo,
                coupon
            );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <returns></returns>
        public bool ApplyDiscountToOrderLine(
            Guid lineKey,
            IDiscount discount,
            string storeAlias = null,
            string coupon = null,
            OrderInfo orderInfo = null
        )
        {
            orderInfo = orderInfo ?? GetOrder(storeAlias);
            OrderLine orderLine
                = orderInfo.OrderLines.FirstOrDefault(line => line.Key == lineKey)
                as OrderLine;

            if (orderLine == null)
            {
                throw new OrderLineNotFoundException($"Unable to find order line: {lineKey}");
            }

            return ApplyDiscountToOrderLine(
                orderLine,
                discount,
                orderInfo,
                coupon
            );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <returns></returns>
        private bool ApplyDiscountToOrderLine(
            OrderLine orderLine,
            IDiscount discount,
            OrderInfo orderInfo,
            string coupon = null
        )
        {
            _log.Debug("Applying discount to orderline");

            if (orderLine.Discount != null)
            {
                if (orderLine.Discount.DiscountItems.Contains(orderLine.ProductKey))
                {
                    if (IsBetterDiscount(orderLine, discount))
                    {
                        orderLine.Discount = new OrderedDiscount(discount);
                        orderLine.Coupon = coupon;

                        UpdateOrderAndOrderInfo(orderInfo);

                        _log.Debug("Successfully applied discount to orderline");
                        return true;
                    }
                }
                else
                {
                    orderLine.Discount = null;
                }
                
            }
            else
            {
                if (orderInfo.Discount != null)
                {
                    if (orderInfo.Discount.DiscountItems.Contains(orderLine.ProductKey))
                    {
                        // Apply cart discount on line for comparison with new discount
                        orderLine.Discount = orderInfo.Discount;
                        orderLine.Coupon = orderInfo.Coupon;
                        return true;
                    }
                }
                                  
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

            RemoveDiscountFromOrderLine(orderLine);

            UpdateOrderAndOrderInfo(orderInfo);
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
            if (orderInfo.Discount == null)
            {
                return true;
            }

            if (orderInfo.Discount.Amount.Type == discount.Amount.Type)
            {
                return discount.CompareTo(orderInfo.Discount) > 0;
            }

            var oldDiscount = orderInfo.Discount;
            var oldTotal = orderInfo.ChargedAmount.Value;

            orderInfo.Discount = new OrderedDiscount(discount);

            var result = orderInfo.ChargedAmount.Value < oldTotal;

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

            (discount as Models.Discounts.Discount)?.OnCouponApply();
        }

        private void ApplyGlobalDiscounts(OrderInfo orderInfo)
        {

        }

        /// <summary>
        /// Verifies all <see cref="Discount"/>'s match their constraints.
        /// Removes non-compliant <see cref="Discount"/>'s
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
                    if (line.Discount?.Constraints.IsValid(storeAlias, total) == false || !line.Discount.DiscountItems.Contains(line.ProductKey))
                    {
                        RemoveDiscountFromOrderLine(line);
                    }
                }
                
            }
        }
    }
}
