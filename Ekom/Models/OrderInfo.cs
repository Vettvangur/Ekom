using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Models.OrderedObjects;
using Ekom.Services;
using Ekom.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.Models
{
    /// <inheritdoc />
    class OrderInfo : IOrderInfo
    {
        public StoreInfo StoreInfo { get; }

        private readonly OrderData _orderData;
        internal OrderData OrderDataClone() => _orderData.Clone() as OrderData;

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
                _hangfireJobs = orderInfoJObject[nameof(HangfireJobs)]?.ToObject<List<string>>();
            }
        }

        /// <inheritdoc />
        public OrderedDiscount Discount { get; internal set; }
        /// <inheritdoc />
        public string Coupon { get; internal set; }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public IReadOnlyCollection<IOrderLine> OrderLines => orderLines.AsReadOnly();

        public OrderedShippingProvider ShippingProvider { get; set; }
        public OrderedPaymentProvider PaymentProvider { get; set; }

        /// <inheritdoc />
        public int TotalQuantity
        {
            get
            {
                return OrderLines?.Any() == true
                    ? OrderLines.Sum(x => x.Quantity)
                    : 0;
            }
        }

        public CustomerInfo CustomerInformation { get; } = new CustomerInfo();

        /// <inheritdoc />
        public ICalculatedPrice OrderLineTotal
        {
            get
            {
                var amount = OrderLines.Sum(line => line.Amount.Value);

                amount = Calculator.EkomRounding(amount, Configuration.Current.OrderVatCalculationRounding);

                return new CalculatedPrice(amount, StoreInfo.Currency);
            }
        }

        private Price LinePriceWithOrderDiscount(IOrderLine line)
        {
            var discount = Discount;
            if (discount?.DiscountItems.Any() == true)
            {
                // Filters order discounts to their applicable DiscountItems
                if (!OrderService.IsDiscountApplicable(this, line, discount))
                {
                    discount = null;
                }
            }

            return new Price(
                line.Amount.OriginalValue,
                StoreInfo.Currency,
                line.Vat,
                StoreInfo.VatIncludedInPrice,
                discount,
                line.Quantity
            );
        }

        /// <inheritdoc />
        public ICalculatedPrice SubTotal
        {
            get
            {
                var amount = OrderLines.Sum(line =>
                {
                    if (line.Discount == null)
                    {
                        var lineWithOrderDiscount = LinePriceWithOrderDiscount(line);

                        return lineWithOrderDiscount.AfterDiscount.Value;
                    }

                    return line.Amount.AfterDiscount.Value;
                });

                amount = Calculator.EkomRounding(amount, Configuration.Current.OrderVatCalculationRounding);

                return new CalculatedPrice(amount, StoreInfo.Currency);
            }
        }

        /// <inheritdoc />
        public ICalculatedPrice Vat
        {
            get
            {
                // This ensures correctness even with per order rounding
                var amount = SubTotal.Value - OrderLines.Sum(line => line.Amount.WithoutVat.Value);

                return new CalculatedPrice(amount, StoreInfo.Currency);
            }
        }

        /// <inheritdoc />
        public ICalculatedPrice ChargedVat
        {
            get
            {
                var amount = ChargedAmount.Value;

                amount -= OrderLines.Sum(line => line.Amount.WithoutVat.Value);

                if (ShippingProvider != null)
                {
                    amount -= ShippingProvider.Price.WithoutVat.Value;
                }

                if (PaymentProvider != null)
                {
                    amount -= PaymentProvider.Price.WithoutVat.Value;
                }

                return new CalculatedPrice(amount, StoreInfo.Currency);
            }
        }

        /// <inheritdoc />
        public ICalculatedPrice GrandTotal
        {
            get
            {
                var amount = OrderLines.Sum(line =>
                {
                    if (line.Discount == null)
                    {
                        var lineWithOrderDiscount = LinePriceWithOrderDiscount(line);

                        return lineWithOrderDiscount.Value;
                    }

                    return line.Amount.Value;
                });

                amount = Calculator.EkomRounding(amount, Configuration.Current.OrderVatCalculationRounding);

                return new CalculatedPrice(amount, StoreInfo.Currency);
            }
        }

        /// <inheritdoc />
        public ICalculatedPrice DiscountAmount
            => new CalculatedPrice(
                OrderLineTotal.Value - GrandTotal.Value,
                StoreInfo.Currency);

        /// <inheritdoc />
        public ICalculatedPrice ChargedAmount
        {
            get
            {
                var amount = OrderLines.Sum(line =>
                {
                    if (line.Discount == null
                    // This is for OrderService.Discounts.IsBetterDiscount, 
                    // allowing us to temporarily apply an exclusive discount to the order
                    // without removing discounts from all orderlines.
                    // In normal use an exclusive order discount will never be applied to an order 
                    // at the same time as OrderLines have a discount applied.
                    || Discount?.Stackable == false)
                    {
                        var lineWithOrderDiscount = LinePriceWithOrderDiscount(line);

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

                amount = Calculator.EkomRounding(amount, Configuration.Current.OrderVatCalculationRounding);

                return new CalculatedPrice(amount, StoreInfo.Currency);
            }
        }

        /// <inheritdoc />
        public DateTime CreateDate
        {
            get
            {
                return _orderData.CreateDate;
            }
        }
        /// <inheritdoc />
        public DateTime UpdateDate
        {
            get
            {
                return _orderData.UpdateDate;
            }
        }
        /// <inheritdoc />
        public DateTime? PaidDate
        {
            get
            {
                return _orderData.PaidDate;
            }
        }

        /// <inheritdoc />
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
                var orderLineLink = line[nameof(OrderLine.OrderlineLink)] != null
                    ? (Guid)line[nameof(OrderLine.OrderlineLink)]
                    : Guid.Empty;
                var productJson = line[nameof(OrderLine.Product)].ToString();
                var discount = line[nameof(OrderLine.Discount)]?.ToObject<OrderedDiscount>();
                var orderLineInfo = line[nameof(OrderLine.OrderLineInfo)]?.ToObject<OrderLineInfo>();
                var orderLine = new OrderLine(
                    lineId,
                    quantity,
                    productJson,
                    this,
                    orderLineInfo,
                    discount,
                    orderLineLink);

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
