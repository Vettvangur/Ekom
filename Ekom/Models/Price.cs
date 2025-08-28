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
        Quantity = jObject[nameof(Quantity)]?.Value<int>() ?? 1;
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
    {
        OriginalValue = price;
        Currency = currency;
        _storeVAT = vat;
        _storeVatIncludedInPrices = vatIncludedInPrice;
        Discount = discount;
        Quantity = quantity;
        DiscountAlwaysBeforeVAT = discountAlwaysBeforeVat;
        HasDiscount = discount != null;
    }

    bool perUnit => Configuration.Instance.VatRoundingScope == VatRoundingScope.PerUnit;

    private CalculatedPrice CreateSimplePrice(decimal price)
        => new CalculatedPrice(price, Currency);

    public object Clone() => MemberwiseClone();

    public decimal OriginalValue { get; }
    public decimal Quantity { get; }
    public bool HasDiscount { get; }

    public ICalculatedPrice BeforeDiscount
    {
        get
        {
            var (net, vat, gross) = ComputeLineTotals(applyDiscount: false);
            return CreateSimplePrice(_storeVatIncludedInPrices ? gross : net);
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
            if (OriginalValue == Math.Floor(OriginalValue))
            {
                price = Math.Round(price);
            }

            return price;
        }
    }

    private (decimal Net, decimal Vat, decimal Gross) ComputeLineTotals(bool applyDiscount)
    {
        decimal unit = applyDiscount ? DiscountedValue : OriginalValue;
        string iso = Currency.ISOCurrencySymbol;

        if (_storeVatIncludedInPrices)
        {
            if (perUnit)
            {
                // VAT included + PerUnit: split per unit, preserve sticker gross
                var (unitNet, unitVat) = Calculator.SplitVatFromGrossPerUnit(unit, _storeVAT, iso);
                var net = unitNet * Quantity;
                var vat = unitVat * Quantity;
                var gross = (unitNet + unitVat) * Quantity;
                return (net, vat, gross);
            }
            else // PerTotal
            {
                var grossRaw = unit * Quantity;
                var net = Calculator.WithoutVat(grossRaw, _storeVAT, iso);
                var vat = Calculator.VatAmountFromWithoutVat(net, _storeVAT, iso);
                var gross = net + vat;
                return (net, vat, gross);
            }
        }
        else
        {
            // VAT-exclusive pricing
            if (perUnit)
            {
                // ✅ VAT excluded + PerUnit:
                // round each unit's gross, then multiply
                var unitGross = Calculator.WithVat(unit, _storeVAT, iso); // applies ISK rounding policy
                var unitVat = unitGross - unit;

                var net = unit * Quantity;            // net per unit × qty
                var vat = unitVat * Quantity;         // VAT per unit × qty
                var gross = unitGross * Quantity;       // gross per unit × qty
                return (net, vat, gross);
            }
            else // PerTotal
            {
                var net = unit * Quantity;
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
