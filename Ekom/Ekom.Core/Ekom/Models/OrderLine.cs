using Ekom.Services;
using Ekom.Utilities;
using System.Xml.Serialization;

namespace Ekom.Models;

/// <summary>
/// 
/// </summary>
public class OrderLine : IOrderLine
{
    public Guid ProductKey => Product.Key;
    public Guid? VariantKey => Variant?.Key;
    public OrderedVariant? Variant => Product.VariantGroups?.FirstOrDefault()?.Variants?.FirstOrDefault();

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public IOrderInfo OrderInfo { get; }

    /// <summary>
    /// 
    /// </summary>
    public Guid Key { get; internal set; }

    public OrderedProduct Product { get; internal set; }
    /// <summary>
    /// 
    /// </summary>
    public decimal Quantity { get; internal set; }
    /// <summary>
    /// Optional Information Attached To Order
    /// </summary>
    public OrderLineInfo OrderLineInfo { get; } = new OrderLineInfo();
    /// <summary>
    /// </summary>
    public OrderedDiscount Discount { get; internal set; }
    /// <summary>
    /// 
    /// </summary>
    public string Coupon { get; internal set; }

    /// <summary>
    /// 
    /// </summary>
    public OrderLineSettings Settings { get; internal set; } = new OrderLineSettings();

    private IPrice? _amount;

    public IPrice Amount
    {
        get
        {
            return CalculateAmount();
        }
    }

    public void InvalidateAmount() => _amount = null;

    /// <summary>
    /// Line price with discount and quantity and variant modifications
    /// </summary>
    private IPrice CalculateAmount() { 

        var orderlinePrice = Variant != null ? Variant.Price : Product.Price;

        OrderedDiscount? discount = orderlinePrice.Discount;

        if (OrderInfo?.Discount != null &&
            DiscountApplicability.MatchesLineTargets(this, OrderInfo.Discount))
        {
            if (Product.Price.HasDiscount && OrderInfo.Discount.Stackable)
            {
                discount = OrderInfo.Discount;
            }
            else if (!Product.Price.HasDiscount && OrderInfo.Discount != null)
            {
                discount = OrderInfo.Discount;
            }
            else if ((Product.Price.Discount?.Amount ?? 0) < OrderInfo.Discount?.Amount)
            {
                discount = OrderInfo.Discount;
            }
        }

        Discount = discount;

        decimal _price = Discount != null && Discount.Stackable ? orderlinePrice.Value : orderlinePrice.OriginalValue;

        //if (Product.VariantGroups != null && Product.VariantGroups.Any())
        //{
        //    IEnumerable<OrderedVariant> variants = Product.VariantGroups.SelectMany(x => x.Variants);

        //    foreach (OrderedVariant? v in variants)
        //    {
        //        _price += (v.Price.OriginalValue - _price);

        //    }
        //}

        return new Price(
            _price,
            OrderInfo?.StoreInfo.Currency,
            Vat,
            OrderInfo.StoreInfo.VatIncludedInPrice,
            discount,
            Quantity);
        
    }

    public decimal Vat
    {
        get
        {
            OrderedVariantGroup? variantGroup = Product.VariantGroups.FirstOrDefault(x => x.Properties.ContainsKey("vat"));

            if (variantGroup != null && !string.IsNullOrEmpty(variantGroup.Properties.GetPropertyValue("vat", OrderInfo.StoreInfo.Alias)))
            {
                string vatVal = variantGroup.Properties.GetPropertyValue("vat", OrderInfo.StoreInfo.Alias);
                if (VatParser.TryParsePercentageRate(vatVal, out var vat))
                {
                    return vat;
                }
            }

            return Product.Vat;
        }
    }

    /// <summary>
    /// ctor
    /// </summary>
    public OrderLine(
        Guid lineId,
        decimal quantity,
        string productJson,
        OrderInfo orderInfo,
        OrderLineInfo orderLineInfo,
        OrderedDiscount discount,
        OrderLineSettings settings)
    {
        Key = lineId;
        Quantity = quantity;
        OrderInfo = orderInfo;
        OrderLineInfo = orderLineInfo;
        Product = new OrderedProduct(productJson, orderInfo.StoreInfo);
        Discount = discount ?? Variant?.Price.Discount ?? Product.Price.Discount;
        Settings = settings;
    }

    /// <summary>
    /// ctor
    /// </summary>
    public OrderLine(
        IProduct product,
        decimal quantity,
        Guid lineId,
        OrderInfo orderInfo,
        Dictionary<string, string> orderLineData,
        IVariant? variant = null,
        OrderDynamicRequest? orderDynamic = null)
    {
        OrderInfo = orderInfo;
        Quantity = quantity;
        Key = lineId;
        Product = new OrderedProduct(product, variant, orderInfo.StoreInfo, orderDynamic);
        Discount = Variant?.Price.Discount ?? Product.Price.Discount;
        OrderLineInfo = new OrderLineInfo()
        {
            Properties = orderLineData
        };
        if (orderDynamic != null)
        {
            Settings = new OrderLineSettings()
            {
                CountToTotal = orderDynamic.CountToTotal,
                Link = orderDynamic.OrderLineLink
            };
        }
    }
}
