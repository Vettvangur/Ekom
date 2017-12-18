using System;
using System.Collections.Generic;
using System.Linq;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using log4net;
using System.Reflection;

namespace Ekom.Models
{
    /// <summary>
    /// Order/Cart
    /// </summary>
    public class OrderInfo : IOrderInfo
    {
        private OrderData _orderData;
        private Store _store;
        private StoreInfo _storeInfo;
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

        public OrderInfo(OrderData orderData)
        {
            _orderData = orderData;

            if (!string.IsNullOrEmpty(orderData.OrderInfo))
            {
                var orderInfoJObject = JObject.Parse(orderData.OrderInfo);

                _storeInfo = CreateStoreInfoFromJson(orderInfoJObject);
                OrderLines = CreateOrderLinesFromJson(orderInfoJObject);
                ShippingProvider = CreateShippingProviderFromJson(orderInfoJObject);
                PaymentProvider = CreatePaymentProviderFromJson(orderInfoJObject);
                CustomerInformation = CreateCustomerInformationFromJson(orderInfoJObject);
            } else
            {
                _storeInfo = new StoreInfo(API.Store.Current.GetStore(orderData.StoreAlias));
            }

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

        public List<OrderLine> OrderLines = new List<OrderLine>();

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
        private OrderData orderData;

        public Price OrderLineTotal
        {
            get
            {
                // OrderLines Exlude OrderDiscount included
                var amount = OrderLines.Sum(x => x.Amount.Value);

                return new Price(amount, StoreInfo);
            }
        }

        public Price SubTotal
        {
            get
            {
                // OrderLines with OrderDiscount included
                var amount = OrderLines.Sum(x => x.Amount.Value);

                return new Price(amount, StoreInfo);
            }
        }

        /// <summary>
        /// SimplePrice object for total value of all orderlines.
        /// </summary>
        public Price ChargedAmount
        {
            get
            {
                var amount = SubTotal.Value;

                if (ShippingProvider != null)
                {
                    amount = amount + ShippingProvider.Price.WithVat.Value;
                }

                if (PaymentProvider != null)
                {
                    amount = amount + PaymentProvider.Price.WithVat.Value;
                }

                //return new SimplePrice(false, amount, StoreInfo.Culture, StoreInfo.Vat, StoreInfo.VatIncludedInPrice);
                return new Price(amount, StoreInfo);
            }
        }
        public StoreInfo StoreInfo
        {
            get
            {
                if (_storeInfo == null)
                {
                    return new StoreInfo(_store);
                } else
                {
                    return _storeInfo;
                }
                
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

        private List<OrderLine> CreateOrderLinesFromJson(JObject orderInfoJObject)
        {
            var orderLines = new List<OrderLine>();

            var orderLinesArray = (JArray)orderInfoJObject["OrderLines"];

            var storeJson = orderInfoJObject["StoreInfo"].ToString();

            var storeInfo = JsonConvert.DeserializeObject<StoreInfo>(storeJson);

            foreach (var line in orderLinesArray)
            {
                var lineId = (Guid)line["Id"];
                var quantity = (int)line["Quantity"];
                var productJson = line["Product"].ToString();

                var orderLine = new OrderLine(lineId, quantity, productJson, storeInfo);

                orderLines.Add(orderLine);
            }

            return orderLines;
        }

        private OrderedShippingProvider CreateShippingProviderFromJson(JObject orderInfoJObject)
        {
 
            if (orderInfoJObject["ShippingProvider"] != null)
            {
                var shippingProviderJson = orderInfoJObject["ShippingProvider"].ToString();

                if (!string.IsNullOrEmpty(shippingProviderJson))
                {
                    var shippingProviderObject = JObject.Parse(shippingProviderJson);

                    if (shippingProviderObject != null)
                    {
                        var storeJson = orderInfoJObject["StoreInfo"].ToString();

                        var storeInfo = JsonConvert.DeserializeObject<StoreInfo>(storeJson);

                        var p = new OrderedShippingProvider(shippingProviderObject, storeInfo);

                        return p;
                    }
                }
            }

            return null;
        }

        private OrderedPaymentProvider CreatePaymentProviderFromJson(JObject orderInfoJObject)
        {

            if (orderInfoJObject["PaymentProvider"] != null)
            {
                var paymentProviderJson = orderInfoJObject["PaymentProvider"].ToString();

                if (!string.IsNullOrEmpty(paymentProviderJson))
                {
                    var paymentProviderObject = JObject.Parse(paymentProviderJson);

                    if (paymentProviderObject != null)
                    {
                        var storeJson = orderInfoJObject["StoreInfo"].ToString();

                        var storeInfo = JsonConvert.DeserializeObject<StoreInfo>(storeJson);

                        var p = new OrderedPaymentProvider(paymentProviderObject, storeInfo);

                        return p;
                    }
                }
            }

            return null;
        }

        private CustomerInfo CreateCustomerInformationFromJson(JObject orderInfoJObject)
        {

            if (orderInfoJObject["CustomerInformation"] != null)
            {
                var customerInfoJson = orderInfoJObject["CustomerInformation"].ToString();

                if (!string.IsNullOrEmpty(customerInfoJson))
                {
                    var customerInfo = JsonConvert.DeserializeObject<CustomerInfo>(customerInfoJson);

                    return customerInfo;
                }
            }

            return null;
        }
        private StoreInfo CreateStoreInfoFromJson(JObject orderInfoJObject)
        {

            if (orderInfoJObject["StoreInfo"] != null)
            {
                var storeInfoJson = orderInfoJObject["StoreInfo"].ToString();

                if (!string.IsNullOrEmpty(storeInfoJson))
                {
                    var storeInfo = JsonConvert.DeserializeObject<StoreInfo>(storeInfoJson);

                    return storeInfo;
                }
            }

            return null;
        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
