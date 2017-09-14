using System;
using System.Collections.Generic;
using System.Linq;
using uWebshop.Helpers;
using uWebshop.Interfaces;
using uWebshop.Models.Data;

namespace uWebshop.Models
{
    public class OrderInfo : IOrderInfo
    {
        private OrderData _orderData;
        private Store _store;

        public OrderInfo(OrderData orderData, Store store)
        {
            _orderData = orderData;
            _store = store;
        }

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

        public int Quantity
        {
            get
            {
                return OrderLines != null && OrderLines.Any() ? OrderLines.Sum(x => x.Quantity) : 0;
            }
        }
        public SimplePrice ChargedAmount
        {
            get
            {
                var amount = OrderLines.Sum(x => x.Product.Price.WithVat.Value * x.Quantity);

                return new SimplePrice(false, amount, StoreInfo.Culture, StoreInfo.Vat, StoreInfo.VatIncludedInPrice);
            }
        }
        public StoreInfo StoreInfo
        {
            get
            {
                return new StoreInfo(_store);
            }
        }
        public DateTime CreateDate
        {
            get
            {
                return _orderData.CreateDate;
            }
        }
        public DateTime UpdateDate
        {
            get
            {
                return _orderData.UpdateDate;
            }
        }
        public DateTime PaidDate
        {
            get
            {
                return _orderData.PaidDate;
            }
        }

        public OrderStatus OrderStatus
        {
            get
            {
                OrderStatus _orderStatus;

                if (!OrderStatus.TryParse(_orderData.OrderStatus, out _orderStatus))
                {
                    throw new Exception("OrderStatus could not be parsed for OrderInfo");
                }

                return _orderStatus;
            }
        }
    }
}
