using Ekom.Events;
using Ekom.Utilities;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace Ekom.Models;

/// <summary>
/// Price of item including all data to fully calculate 
/// before and after VAT/Discount.
/// </summary>
public class Price : IPrice
{
    public OrderedDiscount Discount { get; }

    private decimal _storeVAT { get; }
    private bool _storeVatIncludedInPrices { get; }
    public CurrencyModel Currency { get; }

    /// <summary>
    /// Use to ensure that flat discounts are applied before VAT when VAT is included in price.
    /// </summary>
    public bool DiscountAlwaysBeforeVAT { get; set; }

    /// <summary>
    /// ctor from JObject
    /// </summary>
    public Price(
        JToken jObject,
        CurrencyModel currency,
        decimal vat,
        bool vatIncludedInPrice
    )
    {
        Currency = currency;
        _storeVAT = vat;
        _storeVatIncludedInPrices = vatIncludedInPrice;
        OriginalValue = jObject[nameof(OriginalValue)].Value<decimal>();
        Discount = jObject[nameof(Discount)]?.ToObject<OrderedDiscount>();
        Quantity = jObject[nameof(Quantity)]?.Value<decimal>() ?? 1;
        var discountedQuantity = jObject[nameof(DiscountedQuantity)]?.Value<decimal>()
            ?? (Discount != null ? Quantity : 0);
        DiscountedQuantity = Discount == null
            ? 0
            : Math.Min(Math.Max(discountedQuantity, 0), Math.Max(Quantity, 0));
        DiscountAlwaysBeforeVAT = jObject[nameof(DiscountAlwaysBeforeVAT)]?.Value<bool>() ?? false;
        HasDiscount = Discount != null;
    }

    /// <summary>
    /// ctor
    /// </summary>
    public Price(
        string price,
        CurrencyModel currency,
        decimal vat,
        bool vatIncludedInPrice,
        OrderedDiscount? discount = null,
        decimal quantity = 1,
        bool discountAlwaysBeforeVat = false
    )
        : this(
            price,
            currency,
            vat,
            vatIncludedInPrice,
            discount,
            quantity,
            discountedQuantity: null,
            discountAlwaysBeforeVat)
    {
    }

    public Price(
        string price,
        CurrencyModel currency,
        decimal vat,
        bool vatIncludedInPrice,
        OrderedDiscount? discount,
        decimal quantity,
        decimal? discountedQuantity,
        bool discountAlwaysBeforeVat = false
    )
        : this(
            decimal.Parse(
                string.IsNullOrEmpty(price)
                    ? "0"
                    : price?.Replace(',', '.') ?? "0",
                NumberStyles.Any,
                CultureInfo.InvariantCulture),
            currency,
            vat,
            vatIncludedInPrice,
            discount,
            quantity,
            discountedQuantity,
            discountAlwaysBeforeVat)
    {
    }

    /// <summary>
    /// ctor
    /// </summary>
    public Price(
        decimal price,
        CurrencyModel currency,
        decimal vat,
        bool vatIncludedInPrice,
        OrderedDiscount? discount = null,
        decimal quantity = 1,
        bool discountAlwaysBeforeVat = false
    )
        : this(
            price,
            currency,
            vat,
            vatIncludedInPrice,
            discount,
            quantity,
            discountedQuantity: null,
            discountAlwaysBeforeVat)
    {
    }

    public Price(
        decimal price,
        CurrencyModel currency,
        decimal vat,
        bool vatIncludedInPrice,
        OrderedDiscount? discount,
        decimal quantity,
        decimal? discountedQuantity,
        bool discountAlwaysBeforeVat = false
    )
    {
        OriginalValue = price;
        Currency = currency;
        _storeVAT = vat;
        _storeVatIncludedInPrices = vatIncludedInPrice;
        Discount = discount;
        Quantity = quantity;
        DiscountedQuantity = discount == null
            ? 0
            : Math.Min(Math.Max(discountedQuantity ?? quantity, 0), Math.Max(quantity, 0));
        DiscountAlwaysBeforeVAT = discountAlwaysBeforeVat;
        HasDiscount = discount != null;
    }

    bool perUnit => Configuration.Instance.VatRoundingScope == VatRoundingScope.PerUnit;

    private CalculatedPrice CreateSimplePrice(decimal price)
        => new CalculatedPrice(price, Currency);

    public object Clone() => MemberwiseClone();

    public decimal OriginalValue { get; }
    public decimal Quantity { get; }
    public decimal DiscountedQuantity { get; }
    public bool HasDiscount { get; }

    public ICalculatedPrice BeforeDiscount
    {
        get
        {
            var (net, vat, gross) = ComputeLineTotals(applyDiscount: false);
            return CreateSimplePrice(_storeVatIncludedInPrices ? gross : net);
        }
    }

    public ICalculatedPrice BeforeDiscountWithOutVat
    {
        get
        {
            var (net, _, _) = ComputeLineTotals(applyDiscount: false);
            return CreateSimplePrice(net);
        }
    }

    public ICalculatedPrice AfterDiscount
    {
        get
        {
            var (net, vat, gross) = ComputeLineTotals(applyDiscount: true);
            return CreateSimplePrice(_storeVatIncludedInPrices ? gross : net);
        }
    }
    public ICalculatedPrice AfterDiscountWithOutVat
    {
        get
        {
            var (net, _, _) = ComputeLineTotals(applyDiscount: true);
            return CreateSimplePrice(net);
        }
    }
    public ICalculatedPrice WithoutVat
    {
        get
        {
            var (net, _, _) = ComputeLineTotals(applyDiscount: true);
            return CreateSimplePrice(net);
        }
    }

    public ICalculatedPrice WithVat
    {
        get
        {
            var (_, _, gross) = ComputeLineTotals(applyDiscount: true);
            return CreateSimplePrice(gross);
        }
    }

    public decimal Value => WithVat.Value;

    public ICalculatedPrice Vat
    {
        get
        {
            var (_, vat, _) = ComputeLineTotals(applyDiscount: true);
            return CreateSimplePrice(vat);
        }
    }

    public ICalculatedPrice DiscountAmount
        => CreateSimplePrice(BeforeDiscount.Value - AfterDiscount.Value);

    private decimal DiscountedValue
    {
        get
        {
            decimal price = OriginalValue;

            if (Discount != null)
            {
                switch (Discount.Type)
                {
                    case DiscountType.Fixed:
                        if (DiscountAlwaysBeforeVAT && _storeVatIncludedInPrices)
                        {
                            price = Calculator.WithoutVat(price, _storeVAT, Currency.ISOCurrencySymbol);
                        }
                        price -= Discount.Amount;
                        if (DiscountAlwaysBeforeVAT && _storeVatIncludedInPrices)
                        {
                            price = Calculator.WithVat(price, _storeVAT, Currency.ISOCurrencySymbol);
                        }
                        break;

                    case DiscountType.Percentage:
                        price -= price * Discount.Amount;
                        break;
                }
            }

            // If the OriginalValue has no decimals, round DiscountedValue to integer
            //if (OriginalValue == Math.Floor(OriginalValue))
            //{
            //    price = Math.Round(price);
            //}

            return Math.Max(0, price);
        }
    }

    private (decimal Net, decimal Vat, decimal Gross) ComputeLineTotals(bool applyDiscount)
    {
        if (!applyDiscount || Discount == null || DiscountedQuantity >= Quantity)
        {
            return ComputeTotals(applyDiscount ? DiscountedValue : OriginalValue, Quantity);
        }

        if (!perUnit)
        {
            var total = DiscountedValue * DiscountedQuantity
                + OriginalValue * (Quantity - DiscountedQuantity);
            return ComputeTotals(total, 1);
        }

        var discountedTotals = ComputeTotals(DiscountedValue, DiscountedQuantity);
        var fullPriceTotals = ComputeTotals(OriginalValue, Quantity - DiscountedQuantity);
        return (
            discountedTotals.Net + fullPriceTotals.Net,
            discountedTotals.Vat + fullPriceTotals.Vat,
            discountedTotals.Gross + fullPriceTotals.Gross);
    }

    private (decimal Net, decimal Vat, decimal Gross) ComputeTotals(decimal unit, decimal quantity)
    {
        string iso = Currency.ISOCurrencySymbol;

        if (_storeVatIncludedInPrices)
        {
            if (perUnit)
            {
                switch (Configuration.Instance.VatIncludedPerUnitPolicy)
                {
                    case VatIncludedPerUnitPolicy.LineLevelVat:
                        {
                            // 1) derive UNIT net (with currency policy rounding)
                            var unitNet = Calculator.WithoutVat(unit, _storeVAT, iso);
                            // 2) line net
                            var net = unitNet * quantity;
                            // 3) line VAT (recomputed & rounded at line level)
                            var vat = Calculator.VatAmountFromWithoutVat(net, _storeVAT, iso);
                            // 4) line gross (may differ from sticker × qty)
                            var gross = net + vat;
                            return (net, vat, gross);
                        }

                    case VatIncludedPerUnitPolicy.PreserveStickerGross:
                    default:
                        {   
                            //if VAT rate is 0%, still apply currency rounding to the sticker gross per unit
                            if (_storeVAT == 0m)
                            {
                                // WithVat(x, 0) returns currency-rounded amount (e.g., whole krónur for ISK)
                                var roundedUnitGross = Calculator.WithVat(unit, 0m, iso);
                                var grossValue = roundedUnitGross * quantity;

                                // No tax at 0%: net == gross, vat == 0
                                return (grossValue, 0m, grossValue);
                            }

                            // Per-unit residuals; keep sticker gross = unit × qty
                            var (unitNet, unitVat) = Calculator.SplitVatFromGrossPerUnit(unit, _storeVAT, iso);
                            var net = unitNet * quantity;
                            var vat = unitVat * quantity;
                            var gross = unit * quantity; // preserve sticker exactly
                            return (net, vat, gross);
                        }
                }
            }
            else // PerTotal
            {
                var grossRaw = unit * quantity;
                var net = Calculator.WithoutVat(grossRaw, _storeVAT, iso);
                var vat = Calculator.VatAmountFromWithoutVat(net, _storeVAT, iso);
                var gross = net + vat;
                return (net, vat, gross);
            }
        }
        else
        {
            // VAT-exclusive pricing (unchanged)
            if (perUnit)
            {
                var unitGross = Calculator.WithVat(unit, _storeVAT, iso);
                var unitVat = unitGross - unit;

                var net = unit * quantity;
                var vat = unitVat * quantity;
                var gross = unitGross * quantity;
                return (net, vat, gross);
            }
            else
            {
                var net = unit * quantity;
                var vat = Calculator.VatAmountFromWithoutVat(net, _storeVAT, iso);
                var gross = net + vat;
                return (net, vat, gross);
            }
        }
    }
}

/// <summary>
/// An object that contains the calculated price given the provided parameters
/// Also offers a way of printing the value using the provided culture.
/// </summary>
class CalculatedPrice : ICalculatedPrice
{
    //[JsonConstructor]
    //public CalculatedPrice(
    //    string currencyString,
    //    decimal value
    //)
    //{
    //    Value = value;
    //    //CurrencyString = currencyString;
    //}

    private CurrencyModel _currencyCulture;

    public CalculatedPrice(
        decimal price,
        CurrencyModel currencyCulture
)
    {
        Value = price;
        _currencyCulture = currencyCulture;
    }

    /// <summary>
    /// Value with vat if applicable
    /// </summary>
    public decimal Value { get; }

    public virtual string CurrencyString
    {
        get
        {
            CultureInfo ci = CreateCultureInfo(_currencyCulture.CurrencyValue);
            string value = FormatCurrencyValue(ci);

            CurrencyStringEventArgs model = new CurrencyStringEventArgs()
            {
                CultureInfo = ci,
                Value = Value,
                ValueString = value
            };

            CatalogEvents.OnCurrencyStringFormat(this, model);

            return model.ValueString;
        }
    }

    private CultureInfo CreateCultureInfo(string cultureValue)
    {
        CultureInfo ci = new CultureInfo(cultureValue);
        return ci.TwoLetterISOLanguageName.ToUpperInvariant() == "IS"
            ? Configuration.IsCultureInfo
            : ci;
    }

    private string FormatCurrencyValue(CultureInfo cultureInfo)
    {
        return Value.ToString(_currencyCulture.CurrencyFormat, cultureInfo);
    }

}
