using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Models.OrderedObjects;
using Ekom.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.Models
{
    /// <summary>
    /// Order/Cart
    /// </summary>
    class OrderInfo : IOrderInfo
    {
        public StoreInfo StoreInfo { get; }
        private readonly OrderData _orderData;

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

                StoreInfo = CreateStoreInfoFromJson(orderInfoJObject);
                orderLines = CreateOrderLinesFromJson(orderInfoJObject);
                ShippingProvider = CreateShippingProviderFromJson(orderInfoJObject);
                PaymentProvider = CreatePaymentProviderFromJson(orderInfoJObject);
                CustomerInformation = CreateCustomerInformationFromJson(orderInfoJObject);
                Discount = orderInfoJObject[nameof(Discount)]?.ToObject<OrderedDiscount>();
                Coupon = orderInfoJObject[nameof(Coupon)]?.ToObject<string>();
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

        // We need to check for contraints in provider each time its fetched. But this needs to be done more efficently
        private OrderedShippingProvider _shippingProvider;
        public OrderedShippingProvider ShippingProvider
        {

            get
            {
                if (_shippingProvider != null)
                {
                    var provider = API.Providers.Instance.GetShippingProviders(StoreInfo.Alias, CustomerInformation.Customer.Country, GrandTotal.Value).FirstOrDefault(x => x.Key == _shippingProvider.Key);

                    if (provider != null)
                    {
                        return _shippingProvider;
                    }

                }

                return null;
            }
            set
            {
                _shippingProvider = value;
            }
        }
        private OrderedPaymentProvider _paymentProvider;
        public OrderedPaymentProvider PaymentProvider
        {

            get
            {
                if (_paymentProvider != null)
                {
                    var provider = API.Providers.Instance.GetPaymentProviders(StoreInfo.Alias, CustomerInformation.Customer.Country, GrandTotal.Value).FirstOrDefault(x => x.Key == _paymentProvider.Key);

                    if (provider != null)
                    {
                        return _paymentProvider;
                    }

                }

                return null;
            }
            set
            {
                _paymentProvider = value;
            }
        }
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
        /// OrderLines with OrderDiscount excluded and VAT included
        /// </summary>
        public ICalculatedPrice OrderLineTotal
        {
            get
            {
                var amount = OrderLines.Sum(line => line.Amount.Value);

                return new CalculatedPrice(amount, StoreInfo.Currency);
            }
        }

        /// <summary>
        /// OrderLines with OrderDiscount included and Vat left as-is
        /// </summary>
        public ICalculatedPrice SubTotal
        {
            get
            {
                var SumOriginalPrice = orderLines.Sum(line => line.Amount.OriginalValue);
                var amount = OrderLines.Sum(line =>
                {

                    var price
                        = new Price(
                            line.Amount.OriginalValue,
                            StoreInfo.Currency,
                            StoreInfo.Vat,
                            StoreInfo.VatIncludedInPrice,
                            line.Product.ProductDiscount,
                            line.Discount,
                            true,
                            SumOriginalPrice,
                            line.Quantity
                        );

                    return price.AfterDiscount.Value;

                });

                return new CalculatedPrice(amount, StoreInfo.Currency);
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

                return new CalculatedPrice(amount, StoreInfo.Currency);
            }
        }

        /// <summary>
        /// Includes Vat and discounts but without shipping providers and payment providers.
        /// </summary>
        public ICalculatedPrice GrandTotal
        {
            get
            {
                var SumOriginalPrice = orderLines.Sum(line => line.Amount.OriginalValue);
                var amount = OrderLines.Sum(line =>
                {
                    var price = new Price(
                               line.Amount.OriginalValue,
                               StoreInfo.Currency,
                               StoreInfo.Vat,
                               StoreInfo.VatIncludedInPrice,
                               line.Product.ProductDiscount,
                               line.Discount,
                               true,
                               SumOriginalPrice,
                               line.Quantity
                           );

                    return price.Value;

                });

                return new CalculatedPrice(amount, StoreInfo.Currency);
            }
        }

        /// <summary>
        /// Total monetary value of discount in order
        /// </summary>
        public ICalculatedPrice DiscountAmount
            => new CalculatedPrice(OrderLineTotal.Value - SubTotal.Value, StoreInfo.Currency);

        /// <summary>
        /// The end amount charged for all orderlines, including shipping providers, payment providers and discounts.
        /// </summary>
        public ICalculatedPrice ChargedAmount
        {
            get
            {
                var SumOriginalPrice = orderLines.Sum(line => line.Amount.OriginalValue);

                var amount = OrderLines.Sum(line =>
                {

                    var price
                        = new Price(
                            line.Amount.OriginalValue,
                            StoreInfo.Currency,
                            StoreInfo.Vat,
                            StoreInfo.VatIncludedInPrice,
                            line.Product.ProductDiscount,
                            line.Discount,
                            true,
                            SumOriginalPrice,
                            line.Quantity
                        );

                    return price.Value;

                });

                if (ShippingProvider != null)
                {
                    amount += ShippingProvider.Price.Value;
                }

                if (PaymentProvider != null)
                {
                    amount += PaymentProvider.Price.Value;
                }

                return new CalculatedPrice(amount, StoreInfo.Currency);
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
        public DateTime? PaidDate
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

            var orderLinesArray = (JArray)orderInfoJObject[nameof(OrderLines)];

            foreach (var line in orderLinesArray)
            {
                var lineId = (Guid)line[nameof(OrderLine.Key)];
                var quantity = (int)line[nameof(OrderLine.Quantity)];
                var orderLineLink = line["orderLineLink"] != null ? (Guid)line["orderLineLink"] : Guid.Empty;
                var productJson = line[nameof(OrderLine.Product)].ToString();
                var discount = line[nameof(OrderLine.Discount)] != null ? line[nameof(OrderLine.Discount)]?.ToObject<OrderedDiscount>() : null;
                var orderLineInfo = line[nameof(OrderLine.OrderLineInfo)]?.ToObject<OrderLineInfo>();
                var orderLine = new OrderLine(lineId, quantity, productJson, this, orderLineInfo, discount, orderLineLink);

                orderLines.Add(orderLine);
            }

            return orderLines;
        }

        private OrderedShippingProvider CreateShippingProviderFromJson(JObject orderInfoJObject)
        {
            if (orderInfoJObject[nameof(ShippingProvider)] != null)
            {
                var shippingProviderJson = orderInfoJObject[nameof(ShippingProvider)].ToString();

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
            if (orderInfoJObject[nameof(PaymentProvider)] != null)
            {
                var paymentProviderJson = orderInfoJObject[nameof(PaymentProvider)].ToString();

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

        private StoreInfo CreateStoreInfoFromJson(JObject orderInfoJObject)
        {
            if (orderInfoJObject[nameof(StoreInfo)] != null)
            {
                var storeInfoJson = orderInfoJObject[nameof(StoreInfo)].ToString();

                if (!string.IsNullOrEmpty(storeInfoJson))
                {
                    var storeInfoObject = JObject.Parse(storeInfoJson);

                    if (storeInfoObject != null)
                    {
                        var s = new StoreInfo(storeInfoObject);

                        return s;
                    }
                }
            }

            return null;
        }

        private CustomerInfo CreateCustomerInformationFromJson(JObject orderInfoJObject)
        {
            if (orderInfoJObject[nameof(CustomerInformation)] != null)
            {
                var customerInfoJson = orderInfoJObject[nameof(CustomerInformation)].ToString();

                if (!string.IsNullOrEmpty(customerInfoJson))
                {
                    var customerInfo = JsonConvert.DeserializeObject<CustomerInfo>(customerInfoJson);

                    return customerInfo;
                }
            }

            return null;
        }

        //private OrderLineInfo CreateOrderLineInformationFromJson(JObject orderInfoJObject)
        //{
        //    if (orderInfoJObject["OrderLineInfo"] != null)
        //    {
        //        var orderLineInfoJson = orderInfoJObject["OrderLineInfo"].ToString();

        //        if (!string.IsNullOrEmpty(orderLineInfoJson))
        //        {
        //            var orderLineInfo = JsonConvert.DeserializeObject<OrderLineInfo>(orderLineInfoJson);

        //            return orderLineInfo;
        //        }
        //    }

        //    return null;
        //}

        #endregion
    }
}
