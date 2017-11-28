using System;
using System.Collections.Generic;
using System.Linq;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.Data;

namespace Ekom.Models
{
    /// <summary>
    /// Order/Cart
    /// </summary>
    public class OrderInfo : IOrderInfo
    {
        private OrderData _orderData;
        private Store _store;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderData"></param>
        /// <param name="store"></param>
        public OrderInfo(OrderData orderData, Store store)
        {
            _orderData = orderData;
            _store = store;
        }

        /// <summary>
        /// Order UniqueId
        /// </summary>
        public Guid UniqueId
        {
            get
            {
                return _orderData.UniqueId;
            }
        }
        public int ReferenceId
        {
            get
            {
                return _orderData.ReferenceId;
            }
        }
        public string OrderNumber
        {
            get
            {
                return _orderData.OrderNumber;
            }
        }

        public List<OrderLine> OrderLines { get; set; }

        public OrderedShippingProvider ShippingProvider { get; set; }
        public OrderedPaymentProvider PaymentProvider { get; set; }
        /// <summary>
        /// Total count of items and subitems on each order line.
        /// </summary>
        public int Quantity
        {
            get
            {
                return OrderLines != null && OrderLines.Any() ? OrderLines.Sum(x => x.Quantity) : 0;
            }
        }

        public CustomerInfo CustomerInformation = new CustomerInfo();

        /// <summary>
        /// SimplePrice object for total value of all orderlines.
        /// </summary>
        public Price ChargedAmount
        {
            get
            {
                var amount = OrderLines.Sum(x => x.Product.Price.WithVat.Value * x.Quantity);

                if (ShippingProvider != null)
                {
                    amount = amount + ShippingProvider.Price.WithVat.Value;
                }

                //return new SimplePrice(false, amount, StoreInfo.Culture, StoreInfo.Vat, StoreInfo.VatIncludedInPrice);
                return new Price(amount, StoreInfo);
            }
        }
        public StoreInfo StoreInfo
        {
            get
            {
                return new StoreInfo(_store);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public DateTime CreateDate
        {
            get
            {
                return _orderData.CreateDate;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public DateTime UpdateDate
        {
            get
            {
                return _orderData.UpdateDate;
            }
        }
        /// <summary>
        /// Date payment was verified.
        /// </summary>
        public DateTime PaidDate
        {
            get
            {
                return _orderData.PaidDate;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public OrderStatus OrderStatus => _orderData.OrderStatus;
    }
}
