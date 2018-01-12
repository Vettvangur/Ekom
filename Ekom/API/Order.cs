using Ekom.Exceptions;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace Ekom.API
{
    /// <summary>
    /// The Ekom API, get/update/remove operations on orders 
    /// </summary>
    public class Order
    {
        private static Order _current;
        /// <summary>
        /// Order Singleton
        /// </summary>
        public static Order Current
        {
            get
            {
                return _current ?? (_current = Configuration.container.GetInstance<Order>());
            }
        }

        OrderService _orderService => Configuration.container.GetInstance<OrderService>();
        IStoreService _storeSvc => Configuration.container.GetInstance<IStoreService>();

        /// <summary>
        /// Get order using cookie data and ekmRequest store.
        /// Retrieves from session if possible, otherwise from SQL.
        /// </summary>
        /// <returns></returns>
        public OrderInfo GetOrder()
        {
            var store = _storeSvc.GetStoreFromCache();
            return GetOrder(store.Alias);
        }

        /// <summary>
        /// Get order using cookie data and provided store.
        /// Retrieves from session if possible, otherwise from SQL.
        /// </summary>
        /// <returns></returns>
        public OrderInfo GetOrder(string storeAlias)
        {
            return _orderService.GetOrder(storeAlias);
        }
        public void UpdateStatus(string storeAlias, OrderStatus newStatus)
        {
            var order = _orderService.GetOrder(storeAlias);
            _orderService.ChangeOrderStatus(order.UniqueId, newStatus);
        }

        public void UpdateStatus(string storeAlias, OrderStatus newStatus, Guid orderId)
        {
            _orderService.ChangeOrderStatus(orderId, newStatus);
        }

        public OrderInfo AddOrderLine(
            Guid productId,
            IEnumerable<Guid> variantIds,
            int quantity,
            string storeAlias,
            OrderAction? action
        )
        {
            return _orderService.AddOrderLine(productId, variantIds, quantity, storeAlias, action);
        }

        public OrderInfo UpdateCustomerInformation(Dictionary<string,string> form)
        {
            return _orderService.UpdateCustomerInformation(form);
        }

        public OrderInfo UpdateShippingInformation(Guid ShippingProvider, string storeAlias)
        {
            return _orderService.UpdateShippingInformation(ShippingProvider, storeAlias);
        }

        public OrderInfo UpdatePaymentInformation(Guid PaymentProvider, string storeAlias)
        {
            return _orderService.UpdatePaymentInformation(PaymentProvider, storeAlias);
        }

        public OrderInfo RemoveOrderLine(Guid lineId, string storeAlias)
        {
            return _orderService.RemoveOrderLine(lineId, storeAlias);
        }

        public IEnumerable<OrderInfo> GetCompleteCustomerOrders(int customerId)
        {
            return _orderService.GetCompleteCustomerOrders(customerId);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public bool ApplyDiscountToOrder(Guid discountKey)
        {
            var storeAlias = _storeSvc.GetStoreFromCache().Alias;
            return ApplyDiscountToOrder(discountKey, storeAlias);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public bool ApplyDiscountToOrder(Guid discountKey, string storeAlias)
            => _orderService.ApplyDiscountToOrder(discountKey, storeAlias);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="storeAlias"></param>
        public void RemoveDiscountFromOrder()
        {
            var storeAlias = _storeSvc.GetStoreFromCache().Alias;

            RemoveDiscountFromOrder(storeAlias);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="storeAlias"></param>
        public void RemoveDiscountFromOrder(string storeAlias)
            => _orderService.RemoveDiscountFromOrder(storeAlias);

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public bool ApplyDiscountToOrderLine(Guid productKey, Guid discountKey)
        {
            var storeAlias = _storeSvc.GetStoreFromCache().Alias;

            return ApplyDiscountToOrderLine(productKey, discountKey, storeAlias);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public bool ApplyDiscountToOrderLine(Guid productKey, Guid discountKey, string storeAlias)
            => _orderService.ApplyDiscountToOrderLine(productKey, discountKey, storeAlias);

        /// <summary>
        /// 
        /// </summary>
        public void RemoveDiscountFromOrderLine(Guid productKey)
        {
            var storeAlias = _storeSvc.GetStoreFromCache().Alias;

            RemoveDiscountFromOrderLine(productKey, storeAlias);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="productKey"></param>
        /// <param name="storeAlias"></param>
        public void RemoveDiscountFromOrderLine(Guid productKey, string storeAlias)
            => _orderService.RemoveDiscountFromOrderLine(productKey, storeAlias);
    }
}
