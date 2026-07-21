using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ekom.Models;

/// <inheritdoc />
public class OrderInfo : IOrderInfo
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
            JObject orderInfoJObject = JObject.Parse(orderData.OrderInfo);

            if (orderInfoJObject[nameof(Culture)] != null)
            {
                Culture = orderInfoJObject[nameof(Culture)].ToString();
            }

            StoreInfo = CreateStoreInfoFromJson(orderInfoJObject);
            orderLines = CreateOrderLinesFromJson(orderInfoJObject);
            ShippingProvider = CreateShippingProviderFromJson(orderInfoJObject);
            PaymentProvider = CreatePaymentProviderFromJson(orderInfoJObject);
            CustomerInformation = CreateCustomerInformationFromJson(orderInfoJObject);
            Consent = CreateConsentFromJson(orderInfoJObject);
            Tracking = CreateTrackingFromJson(orderInfoJObject);
            Discount = CreateOrderedDiscountFromJson(orderInfoJObject[nameof(Discount)]);
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
    private string _culture;
    public string Culture
    {
        get
        {
            var contextCulture = GetContextCulture();
            if (!string.IsNullOrWhiteSpace(contextCulture))
            {
                _culture = contextCulture;
            }

            if (string.IsNullOrWhiteSpace(_culture))
            {
                _culture = StoreInfo.Culture;
            }

            return _culture;
        }
        set
        {
            _culture = value;
        }
    }

    private static string? GetContextCulture()
    {
        var httpContext = Configuration.Resolver?.GetService<IHttpContextAccessor>()?.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        return httpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture.Name
            ?? CultureInfo.CurrentCulture.Name;
    }

    /// <summary>
    /// Force changes to come through order api
    /// </summary>
    internal List<OrderLine> orderLines = new List<OrderLine>();

    /// <inheritdoc />
    public IReadOnlyCollection<IOrderLine> OrderLines
        => orderLines.Cast<IOrderLine>().ToList();

    public OrderedShippingProvider? ShippingProvider { get; set; }
    public OrderedPaymentProvider? PaymentProvider { get; set; }

    /// <inheritdoc />
    public decimal TotalQuantity
    {
        get
        {
            return OrderLines?.Any() == true
                ? OrderLines.Where(x => x.Settings != null ? x.Settings.CountToTotal : true).Sum(x => x.Quantity)
                : 0;
        }
    }

    public CustomerInfo CustomerInformation { get; set; } = new CustomerInfo();
    public OrderConsent? Consent { get; set; }
    public OrderTracking? Tracking { get; set; }

    /// <inheritdoc />
    public ICalculatedPrice OrderLineTotal
    {
        get
        {
            decimal amount = OrderLines.Sum(line => line.Amount.Value);

            return new CalculatedPrice(amount, StoreInfo.Currency);
        }
    }

    public ICalculatedPrice OrderLineTotalWithOutVat
    {
        get
        {
            decimal amount = OrderLines.Sum(line => line.Amount.WithoutVat.Value);

            return new CalculatedPrice(amount, StoreInfo.Currency);
        }
    }


    private Price LinePriceWithOrderDiscount(IOrderLine line)
    {
        OrderedDiscount? discount = Discount;
        if (discount != null)
        {
            // Filters order discounts to their applicable include/exclude targets.
            if (!DiscountApplicability.MatchesLineTargets(line, discount))
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
    public IPrice SubTotal
    {
        get
        {

            var amount = OrderLines.Sum(x => x.Amount.BeforeDiscount.Value);

            return new Price(amount, StoreInfo.Currency, StoreInfo.Vat, StoreInfo.VatIncludedInPrice);
        }
    }

    /// <inheritdoc />
    public ICalculatedPrice Vat
    {
        get
        {
            var subTotalWithOutVat = OrderLines.Sum(x => x.Amount.WithoutVat.Value);
            var subTotalWithVat = OrderLines.Sum(x => x.Amount.WithVat.Value);

            var vatAmount = (subTotalWithVat - subTotalWithOutVat);

            if (StoreInfo.ApplyVatOnShipping)
            {
                vatAmount += (ShippingProvider?.Price.Vat.Value ?? 0);
            }

            var vat = new CalculatedPrice(vatAmount, StoreInfo.Currency);

            return vat;
        }
    }

    /// <inheritdoc />
    public ICalculatedPrice ChargedVat => Vat;


    /// <inheritdoc />
    public ICalculatedPrice GrandTotal => ChargedAmount;

    public ICalculatedPrice GrandTotalWithOutVat
    {
        get
        {
            decimal amount = OrderLines.Sum(line =>
            {
                if (line.Discount == null)
                {
                    var lineWithOrderDiscount = LinePriceWithOrderDiscount(line);
                    return lineWithOrderDiscount.WithoutVat.Value;  // line net (already rounded per-line)
                }
                return line.Amount.WithoutVat.Value;
            });

            if (ShippingProvider != null) amount += ShippingProvider.Price.WithoutVat.Value;
            if (PaymentProvider != null) amount += PaymentProvider.Price.WithoutVat.Value;

            return new CalculatedPrice(amount, StoreInfo.Currency);
        }
    }

    /// <inheritdoc />
    public ICalculatedPrice DiscountAmount
    {
        get
        {
            var discountAmount = OrderLines.Sum(x => x.Amount.BeforeDiscount.Value - x.Amount.AfterDiscount.Value);

            return new CalculatedPrice(
                discountAmount,
                StoreInfo.Currency);
        }
    }

    public ICalculatedPrice DiscountAmountWithOutVat
    {
        get
        {
            var discountAmount = OrderLines.Sum(x => x.Amount.BeforeDiscountWithOutVat.Value - x.Amount.AfterDiscountWithOutVat.Value);

            return new CalculatedPrice(
                discountAmount,
                StoreInfo.Currency);
        }
    }

    /// <inheritdoc />
    public ICalculatedPrice ProductDiscountAmount
    {
        get
        {
            decimal amount = OrderLines.Sum(line => line.Product.Price.DiscountAmount.Value);

            return new CalculatedPrice(amount, StoreInfo.Currency);
        }
    }

    public ICalculatedPrice ProductDiscountAmountWithOutVat
    {
        get
        {
            decimal amount = OrderLines.Sum(line => (line.Product.Price.BeforeDiscountWithOutVat.Value - line.Product.Price.AfterDiscountWithOutVat.Value));

            return new CalculatedPrice(amount, StoreInfo.Currency);
        }
    }

    /// <inheritdoc />
    public ICalculatedPrice ChargedAmount
    {
        get
        {
            decimal amount = OrderLines.Sum(line =>
            {
                if (line.Discount == null
                // This is for OrderService.Discounts.IsBetterDiscount, 
                // allowing us to temporarily apply an exclusive discount to the order
                // without removing discounts from all orderlines.
                // In normal use an exclusive order discount will never be applied to an order 
                // at the same time as OrderLines have a discount applied.
                || Discount?.Stackable == false)
                {
                    Price lineWithOrderDiscount = LinePriceWithOrderDiscount(line);

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

    public IEnumerable<IProduct> RelatedProducts(int count = 4)
    {
        List<IProduct> relatedProducts = new List<IProduct>();

        IEnumerable<OrderedProduct> products = orderLines.Select(x => x.Product);

        foreach (OrderedProduct? product in products)
        {
            IEnumerable<IProduct> related = product.RelatedProducts();

            relatedProducts.AddRange(related);
        }

        return relatedProducts.Take(count);
    }


    #region JSON Parsing
    private List<OrderLine> CreateOrderLinesFromJson(JObject orderInfoJObject)
    {
        List<OrderLine> orderLines = new List<OrderLine>();

        JArray? orderLinesArray = (JArray)orderInfoJObject[nameof(OrderLines)];

        foreach (JToken line in orderLinesArray)
        {
            Guid lineId = (Guid)line[nameof(OrderLine.Key)];
            var quantity = (decimal)line[nameof(OrderLine.Quantity)];
            OrderLineSettings? settings = line[nameof(OrderLine.Settings)]?.ToObject<OrderLineSettings>();
            string productJson = line[nameof(OrderLine.Product)].ToString();
            OrderedDiscount? discount = CreateOrderedDiscountFromJson(line[nameof(OrderLine.Discount)]);
            OrderLineInfo? orderLineInfo = line[nameof(OrderLine.OrderLineInfo)]?.ToObject<OrderLineInfo>();
            OrderLine orderLine = new OrderLine(
                lineId,
                quantity,
                productJson,
                this,
                orderLineInfo,
                discount,
                settings);

            orderLines.Add(orderLine);
        }

        return orderLines;
    }

    private static OrderedDiscount? CreateOrderedDiscountFromJson(JToken? discountToken)
    {
        if (discountToken == null || discountToken.Type == JTokenType.Null)
        {
            return null;
        }

        JToken? amountToken = discountToken.Type == JTokenType.Object
            ? discountToken[nameof(OrderedDiscount.Amount)]
            : null;
        if (amountToken?.Type == JTokenType.Object
            && TryGetDecimalAmount(amountToken, out decimal amount))
        {
            JObject discountObject = (JObject)discountToken.DeepClone();
            discountObject[nameof(OrderedDiscount.Amount)] = amount;

            discountToken = discountObject;
        }

        try
        {
            return discountToken.ToObject<OrderedDiscount>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetDecimalAmount(JToken amountToken, out decimal amount)
    {
        foreach (string propertyName in new[] { "Value", "Amount", "value", "amount" })
        {
            if (TryReadDecimal(amountToken[propertyName], out amount))
            {
                return true;
            }
        }

        amount = 0;
        return false;
    }

    private static bool TryReadDecimal(JToken? token, out decimal amount)
    {
        if (token?.Type == JTokenType.Integer || token?.Type == JTokenType.Float)
        {
            amount = token.Value<decimal>();
            return true;
        }

        if (token?.Type == JTokenType.String
            && decimal.TryParse(token.Value<string>(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            return true;
        }

        amount = 0;
        return false;
    }

    private OrderedShippingProvider? CreateShippingProviderFromJson(JObject orderInfoJObject)
    {
        if (orderInfoJObject[nameof(ShippingProvider)] != null)
        {
            string shippingProviderJson = orderInfoJObject[nameof(ShippingProvider)].ToString();

            if (!string.IsNullOrEmpty(shippingProviderJson))
            {
                JObject shippingProviderObject = JObject.Parse(shippingProviderJson);

                if (shippingProviderObject != null)
                {
                    OrderedShippingProvider p = new OrderedShippingProvider(shippingProviderObject, StoreInfo);

                    return p;
                }
            }
        }

        return null;
    }

    private OrderedPaymentProvider? CreatePaymentProviderFromJson(JObject orderInfoJObject)
    {
        if (orderInfoJObject[nameof(PaymentProvider)] != null)
        {
            string paymentProviderJson = orderInfoJObject[nameof(PaymentProvider)].ToString();

            if (!string.IsNullOrEmpty(paymentProviderJson))
            {
                JObject paymentProviderObject = JObject.Parse(paymentProviderJson);

                if (paymentProviderObject != null)
                {
                    OrderedPaymentProvider p = new OrderedPaymentProvider(paymentProviderObject, StoreInfo);

                    return p;
                }
            }
        }

        return null;
    }

    private StoreInfo? CreateStoreInfoFromJson(JObject orderInfoJObject)
    {
        if (orderInfoJObject[nameof(StoreInfo)] != null)
        {
            string storeInfoJson = orderInfoJObject[nameof(StoreInfo)].ToString();

            if (!string.IsNullOrEmpty(storeInfoJson))
            {
                JObject storeInfoObject = JObject.Parse(storeInfoJson);

                if (storeInfoObject != null)
                {
                    StoreInfo s = new StoreInfo(storeInfoObject);

                    return s;
                }
            }
        }

        return null;
    }

    private CustomerInfo? CreateCustomerInformationFromJson(JObject orderInfoJObject)
    {
        if (orderInfoJObject[nameof(CustomerInformation)] != null)
        {
            string customerInfoJson = orderInfoJObject[nameof(CustomerInformation)].ToString();

            if (!string.IsNullOrEmpty(customerInfoJson))
            {
                CustomerInfo? customerInfo = JsonConvert.DeserializeObject<CustomerInfo>(customerInfoJson);

                return customerInfo;
            }
        }

        return null;
    }

    private OrderTracking? CreateTrackingFromJson(JObject orderInfoJObject)
    {
        if (orderInfoJObject[nameof(Tracking)] == null)
        {
            return null;
        }

        string trackingJson = orderInfoJObject[nameof(Tracking)]!.ToString();
        if (string.IsNullOrEmpty(trackingJson))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<OrderTracking>(trackingJson);
    }

    private OrderConsent? CreateConsentFromJson(JObject orderInfoJObject)
    {
        if (orderInfoJObject[nameof(Consent)] == null)
        {
            return null;
        }

        string consentJson = orderInfoJObject[nameof(Consent)]!.ToString();
        if (string.IsNullOrEmpty(consentJson))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<OrderConsent>(consentJson);
    }

    public void UpdateOrderlines(List<OrderLine> orderlines)
    {
        this.orderLines = orderlines;
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
