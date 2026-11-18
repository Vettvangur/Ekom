using Ekom.Events;
using Ekom.Utilities;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
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
            var (net, _, gross) = GetTotals().BeforeDiscount;
            return CreateSimplePrice(_storeVatIncludedInPrices ? gross : net);
        }
    }

    public ICalculatedPrice BeforeDiscountWithOutVat
        => CreateSimplePrice(GetTotals().BeforeDiscount.Net);

    public ICalculatedPrice BeforeDiscountWithVat
        => CreateSimplePrice(GetTotals().BeforeDiscount.Gross);

    public ICalculatedPrice AfterDiscount
    {
        get
        {
            var (net, _, gross) = GetTotals().AfterDiscount;
            return CreateSimplePrice(_storeVatIncludedInPrices ? gross : net);
        }
    }

    public ICalculatedPrice AfterDiscountWithOutVat
        => CreateSimplePrice(GetTotals().AfterDiscount.Net);

    public ICalculatedPrice WithoutVat
        => CreateSimplePrice(GetTotals().AfterDiscount.Net);

    public ICalculatedPrice WithVat
        => CreateSimplePrice(GetTotals().AfterDiscount.Gross);

    public decimal Value => GetTotals().AfterDiscount.Gross;

    public ICalculatedPrice Vat
        => CreateSimplePrice(GetTotals().AfterDiscount.Vat);

    public ICalculatedPrice DiscountAmount
    {
        get
        {
            Totals totals = GetTotals();
            var before = _storeVatIncludedInPrices ? totals.BeforeDiscount.Gross : totals.BeforeDiscount.Net;
            var after = _storeVatIncludedInPrices ? totals.AfterDiscount.Gross : totals.AfterDiscount.Net;
            return CreateSimplePrice(before - after);
        }
    }

    /// <summary>
    /// Everything the totals depend on that can change after construction.
    /// </summary>
    private readonly record struct TotalsKey(
        VatRoundingScope RoundingScope,
        VatIncludedPerUnitPolicy PerUnitPolicy,
        Rounding Rounding,
        bool DiscountAlwaysBeforeVat,
        string? CurrencyValue,
        DiscountType? DiscountType,
        decimal? DiscountAmount);

    private sealed record Totals(
        TotalsKey Key,
        (decimal Net, decimal Vat, decimal Gross) BeforeDiscount,
        (decimal Net, decimal Vat, decimal Gross) AfterDiscount);

    // Every price property used to redo the whole VAT calculation, serializing one price ran it about ten times.
    private Totals? _totals;

    private Totals GetTotals()
    {
        Configuration config = Configuration.Instance;
        var key = new TotalsKey(
            config.VatRoundingScope,
            config.VatIncludedPerUnitPolicy,
            config.VatCalculationRounding,
            DiscountAlwaysBeforeVAT,
            Currency?.CurrencyValue,
            Discount?.Type,
            Discount?.Amount);

        Totals? totals = _totals;
        if (totals != null && totals.Key == key)
        {
            return totals;
        }

        bool perUnit = key.RoundingScope == VatRoundingScope.PerUnit;
        totals = new Totals(
            key,
            ComputeLineTotals(applyDiscount: false, perUnit, key.PerUnitPolicy),
            ComputeLineTotals(applyDiscount: true, perUnit, key.PerUnitPolicy));
        _totals = totals;

        return totals;
    }

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

    private (decimal Net, decimal Vat, decimal Gross) ComputeLineTotals(bool applyDiscount, bool perUnit, VatIncludedPerUnitPolicy perUnitPolicy)
    {
        if (!applyDiscount || Discount == null || DiscountedQuantity >= Quantity)
        {
            return ComputeTotals(applyDiscount ? DiscountedValue : OriginalValue, Quantity, perUnit, perUnitPolicy);
        }

        if (!perUnit)
        {
            var total = DiscountedValue * DiscountedQuantity
                + OriginalValue * (Quantity - DiscountedQuantity);
            return ComputeTotals(total, 1, perUnit, perUnitPolicy);
        }

        var discountedTotals = ComputeTotals(DiscountedValue, DiscountedQuantity, perUnit, perUnitPolicy);
        var fullPriceTotals = ComputeTotals(OriginalValue, Quantity - DiscountedQuantity, perUnit, perUnitPolicy);
        return (
            discountedTotals.Net + fullPriceTotals.Net,
            discountedTotals.Vat + fullPriceTotals.Vat,
            discountedTotals.Gross + fullPriceTotals.Gross);
    }

    private (decimal Net, decimal Vat, decimal Gross) ComputeTotals(decimal unit, decimal quantity, bool perUnit, VatIncludedPerUnitPolicy perUnitPolicy)
    {
        string iso = Currency.ISOCurrencySymbol;

        if (_storeVatIncludedInPrices)
        {
            if (perUnit)
            {
                switch (perUnitPolicy)
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
            CultureInfo ci = GetCurrencyCulture(_currencyCulture.CurrencyValue);
            string value = FormatCurrencyValue(ci);

            if (!CatalogEvents.HasCurrencyStringFormatHandlers)
            {
                return value;
            }

            CurrencyStringEventArgs model = new CurrencyStringEventArgs()
            {
                // Handlers have always received their own mutable culture.
                CultureInfo = (CultureInfo)ci.Clone(),
                Value = Value,
                ValueString = value
            };

            CatalogEvents.OnCurrencyStringFormat(this, model);

            return model.ValueString;
        }
    }

    // Constructing a CultureInfo loads its number format data, far too slow to repeat for every formatted price.
    private static readonly ConcurrentDictionary<string, CultureInfo> _currencyCultures = new(StringComparer.Ordinal);

    private static CultureInfo GetCurrencyCulture(string cultureValue)
        => _currencyCultures.GetOrAdd(cultureValue, static value =>
        {
            CultureInfo ci = new CultureInfo(value);
            return CultureInfo.ReadOnly(
                ci.TwoLetterISOLanguageName.ToUpperInvariant() == "IS"
                    ? Configuration.IsCultureInfo
                    : ci);
        });

    private string FormatCurrencyValue(CultureInfo cultureInfo)
    {
        return Value.ToString(_currencyCulture.CurrencyFormat, cultureInfo);
    }

}
