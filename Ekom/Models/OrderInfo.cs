using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Models.OrderedObjects;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ekom.Models
{
    /// <summary>
    /// Order/Cart
    /// </summary>
    class OrderInfo : IOrderInfo
    {
        public StoreInfo StoreInfo { get; }
        private OrderData _orderData;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderData"></param>
        /// <param name="store"></param>
        public OrderInfo(OrderData orderData, IStore store)
            : this(orderData)
        {
            StoreInfo = new StoreInfo(store);
        }

        public OrderInfo(OrderData orderData)
        {
            _orderData = orderData;

            if (!string.IsNullOrEmpty(orderData.OrderInfo))
            {
                var orderInfoJObject = JObject.Parse(orderData.OrderInfo);

                Log.Info("OrderInfo. Creating.. " + orderData.UniqueId);

                StoreInfo = orderInfoJObject["StoreInfo"].ToObject<StoreInfo>();
                orderLines = CreateOrderLinesFromJson(orderInfoJObject);
                ShippingProvider = CreateShippingProviderFromJson(orderInfoJObject);
                PaymentProvider = CreatePaymentProviderFromJson(orderInfoJObject);
                CustomerInformation = CreateCustomerInformationFromJson(orderInfoJObject);

                Discount = orderInfoJObject["Discount"]?.ToObject<OrderedDiscount>();
            }
        }

        /// <summary>
        /// Force changes to come through order api, 
        /// api can then make checks to ensure that a discount is only ever applied to either cart or items, never both.
        /// </summary>
        public OrderedDiscount Discount { get; internal set; }
        /// <summary>
        /// 
        /// </summary>
        public string Coupon { get; internal set; }

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

        /// <summary>
        /// Force changes to come through order api
        /// </summary>
        internal List<OrderLine> orderLines = new List<OrderLine>();

        /// <summary>
        /// Force changes to come through order api, 
        /// ensuring lines are never changed without passing through the correct channels.
        /// </summary>
        public IReadOnlyCollection<IOrderLine> OrderLines => orderLines.AsReadOnly();

        public OrderedShippingProvider ShippingProvider { get; set; }
        public OrderedPaymentProvider PaymentProvider { get; set; }
        /// <summary>
        /// Total count of items and subitems on each order line.
        /// </summary>
        public int TotalQuantity
        {
            get
            {
                return OrderLines?.Any() == true ? OrderLines.Sum(x => x.Quantity) : 0;
            }
        }

        public CustomerInfo CustomerInformation { get; } = new CustomerInfo();

        /// <summary>
        /// OrderLines with OrderDiscount excluded
        /// </summary>
        public ICalculatedPrice OrderLineTotal
        {
            get
            {
                var amount = OrderLines.Sum(line => line.Amount.BeforeDiscount.Value);

                return new CalculatedPrice(amount, StoreInfo.Culture);
            }
        }

        /// <summary>
        /// OrderLines with OrderDiscount included and Vat left as-is
        /// </summary>
        public ICalculatedPrice SubTotal
        {
            get
            {
                var amount = OrderLines.Sum(line =>
                {
                    if (line.Discount == null)
                    {
                        var lineWithOrderDiscount
                            = new Price(
                                line.Amount.OriginalValue,
                                line.Amount.Store,
                                Discount,
                                line.Quantity
                            );

                        return lineWithOrderDiscount.AfterDiscount.Value;
                    }
                    return line.Amount.AfterDiscount.Value;
                });

                return new CalculatedPrice(amount, StoreInfo.Culture);
            }
        }

        /// <summary>
        /// Total amount of value added tax in order.
        /// This counts up all vat whether it's included in item prices or not.
        /// </summary>
        public ICalculatedPrice Vat
        {
            get
            {
                var amount = OrderLines.Sum(line => line.Amount.Vat.Value);

                return new CalculatedPrice(amount, StoreInfo.Culture);
            }
        }

        /// <summary>
        /// Includes Vat and discounts but without shipping providers and payment providers.
        /// </summary>
        public ICalculatedPrice GrandTotal
        {
            get
            {
                var amount = OrderLines.Sum(line =>
                {
                    if (line.Discount == null)
                    {
                        var lineWithOrderDiscount
                            = new Price(
                                line.Amount.OriginalValue,
                                line.Amount.Store,
                                Discount,
                                line.Quantity
                            );

                        return lineWithOrderDiscount.Value;
                    }
                    return line.Amount.Value;
                });

                return new CalculatedPrice(amount, StoreInfo.Culture);
            }
        }

        /// <summary>
        /// The end amount charged for all orderlines, including shipping providers, payment providers and discounts.
        /// </summary>
        public ICalculatedPrice ChargedAmount
        {
            get
            {
                var amount = OrderLines.Sum(line =>
                {
                    if (line.Discount == null)
                    {
                        var lineWithOrderDiscount
                            = new Price(
                                line.Amount.OriginalValue,
                                line.Amount.Store,
                                Discount,
                                line.Quantity
                            );

                        return lineWithOrderDiscount.Value;
                    }
                    return line.Amount.Value;
                });

                if (ShippingProvider != null)
                {
                    amount += ShippingProvider.Price.Value;
                }

                if (PaymentProvider != null)
                {
                    amount += PaymentProvider.Price.Value;
                }

                return new CalculatedPrice(amount, StoreInfo.Culture);
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

        internal List<string> _hangfireJobs { get; set; } = new List<string>();
        public IReadOnlyCollection<string> HangfireJobs
        {
            get => _hangfireJobs.AsReadOnly();

            internal set => _hangfireJobs = value.ToList();
        }

        #region JSON Parsing
        private List<OrderLine> CreateOrderLinesFromJson(JObject orderInfoJObject)
        {
            var orderLines = new List<OrderLine>();

            var orderLinesArray = (JArray)orderInfoJObject["OrderLines"];

            foreach (var line in orderLinesArray)
            {
                var lineId = (Guid)line["Key"];
                var quantity = (int)line["Quantity"];
                var productJson = line["Product"].ToString();
                var discount = line["Discount"]?.ToObject<OrderedDiscount>();

                var orderLine = new OrderLine(lineId, quantity, productJson, this, discount);

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
                        var p = new OrderedShippingProvider(shippingProviderObject, StoreInfo);

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
                        var p = new OrderedPaymentProvider(paymentProviderObject, StoreInfo);

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

        #endregion

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
