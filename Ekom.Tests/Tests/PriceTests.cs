using Ekom.Models;
using Ekom.Tests.Objects;
using Ekom.Utilities;
using Xunit;

namespace Ekom.Tests.Tests;

public class PriceTests
{
    // --- test data: stores × rounding × quantity ---
    public static IEnumerable<object[]> Matrix()
    {
        var stores = new (string Name, IStore Store, string Culture)[]
        {
            ("IS-24-Included", Stores.Store_IS_24Vat_VatIncluded, "is-IS"),
            ("IS-24-Excluded", Stores.Store_IS_24Vat_VatExcluded, "is-IS"),
            ("IS-0-Included",  Stores.Store_IS_0Vat_VatIncluded,  "is-IS"),

            ("US-11-Included", Stores.Store_US_11Vat_VatIncluded, "en-US"),
            ("US-11-Excluded", Stores.Store_US_11Vat_VatExcluded, "en-US"),
            ("US-0-Included",  Stores.Store_US_0Vat_VatIncluded,  "en-US"),
        };

        var quantities = new[] { 1m, 4m };
        var modes = (Rounding[])Enum.GetValues(typeof(Rounding));
        var scopes = new[] { VatRoundingScope.PerUnit, VatRoundingScope.PerTotal };

        foreach (var s in stores)
            foreach (var scope in scopes)
                foreach (var m in modes)
                    foreach (var q in quantities)
                        yield return new object[] { s.Name, s.Store, s.Culture, scope, m, q };
    }

    private static CurrencyModel Currency(string culture, string format = "C") =>
        new CurrencyModel { CurrencyValue = culture, CurrencyFormat = format };

    // faithful to Calculator.PerformVatRounding + EkomRounding
    private static decimal EkomExpected(decimal val, string iso, Rounding rounding, int decimals = 0)
    {
        if (!iso.Equals("ISK", StringComparison.OrdinalIgnoreCase))
            return val; // non-ISK: no rounding

        val = Math.Round(val, decimals + 5, MidpointRounding.AwayFromZero);

        var pow = (decimal)Math.Pow(10, decimals);
        return rounding switch
        {
            Rounding.None => val,
            Rounding.RoundDown => Math.Floor(val * pow) / pow,
            Rounding.RoundUp => Math.Ceiling(val * pow) / pow,
            Rounding.RoundToEven => Math.Round(val, decimals, MidpointRounding.ToEven),
            Rounding.AwayFromZero => Math.Round(val, decimals, MidpointRounding.AwayFromZero),
            _ => throw new ArgumentOutOfRangeException(nameof(rounding))
        };
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Price_Rounding_Matrix(
        string caseName,
        IStore store,
        string culture,
        VatRoundingScope scope,
        Rounding mode,
        decimal qty)
    {
        using var config = new ConfigurationScope(
            ("Ekom:VatCalcRounding", mode.ToString()),
            ("Ekom:VatRoundingScope", scope.ToString())
        );

        const decimal unitPrice = 1538.42m;
        var currency = Currency(culture);
        var iso = currency.ISOCurrencySymbol; // "ISK" for is-IS, "USD" for en-US

        var price = new Price(
            price: unitPrice,
            currency: currency,
            vat: store.Vat,
            vatIncludedInPrice: store.VatIncludedInPrice,
            discount: null,
            quantity: qty,
            discountAlwaysBeforeVat: false
        );

        var withVat = price.WithVat.Value;
        var withoutVat = price.WithoutVat.Value;

        if (store.VatIncludedInPrice)
        {
            if (scope == VatRoundingScope.PerUnit)
            {
                // PerUnit policy: preserve sticker gross per unit
                Assert.Equal(unitPrice * qty, withVat);

                var unitNetRaw = unitPrice / (1m + store.Vat);
                var unitNetRounded = EkomExpected(unitNetRaw, iso, mode, 0);
                var expectedNet = unitNetRounded * qty;
                Assert.Equal(expectedNet, withoutVat);
            }
            else // PerTotal
            {
                // PerTotal policy: round on the total, gross may drift from sticker * qty
                var totalGross = unitPrice * qty;

                // 1) net = round(totalGross / (1+rate))
                var netTotalRaw = totalGross / (1m + store.Vat);
                var expectedNet = EkomExpected(netTotalRaw, iso, mode, 0);
                Assert.Equal(expectedNet, withoutVat);

                // 2) vat = round(net * rate)
                var vatTotalRaw = expectedNet * store.Vat;
                var expectedVat = EkomExpected(vatTotalRaw, iso, mode, 0);

                // 3) gross = net + vat (NOT necessarily equal to unitPrice*qty)
                var expectedWithVat = expectedNet + expectedVat;
                Assert.Equal(expectedWithVat, withVat);
            }
        }
        else
        {
            // Without VAT in price: your existing expectations are still correct
            Assert.Equal(unitPrice * qty, withoutVat);

            if (scope == VatRoundingScope.PerUnit)
            {
                var unitGrossRaw = unitPrice * (1m + store.Vat);
                var unitGrossRounded = EkomExpected(unitGrossRaw, iso, mode, 0);
                var expected = unitGrossRounded * qty;
                Assert.Equal(expected, withVat);
            }
            else // PerTotal
            {
                var totalNet = unitPrice * qty;
                var grossTotalRaw = totalNet * (1m + store.Vat);
                var expected = EkomExpected(grossTotalRaw, iso, mode, 0);
                Assert.Equal(expected, withVat);
            }
        }


        // Invariants (useful guardrails)
        var diff = withVat - withoutVat;
        Assert.Equal(diff, price.Vat.Value, 0); // VAT calc self-consistency at whole-króna resolution
        if (store.Vat == 0m && !iso.Equals("ISK", StringComparison.OrdinalIgnoreCase))
            Assert.Equal(withVat, withoutVat, 5);
    }


    [Fact]
    public void CurrencyModel_Derives_ISO_From_Culture()
    {
        var isk = new CurrencyModel { CurrencyValue = "is-IS", CurrencyFormat = "C" };
        Assert.Equal("ISK", isk.ISOCurrencySymbol);

        var usd = new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" };
        Assert.Equal("USD", usd.ISOCurrencySymbol);
    }

    [Theory]
    [InlineData(1538.42, 0.24, 4)]
    public void Isk_Diff_PerUnit_vs_PerTotal(decimal price, decimal vat, int qty)
    {
        // per-unit then multiply
        var perUnit = RoundISK(CalcWithVat(price, vat), Rounding.RoundToEven) * qty;
        // multiply then round once
        var perTotal = RoundISK(price * qty * (1 + vat), Rounding.RoundToEven);

        Assert.NotEqual(perUnit, perTotal); // exposes the policy difference
    }

    decimal CalcWithVat(decimal val, decimal vat) => val * (1 + vat);

    // faithful to Calculator: normalize then rounding with 0 decimals for ISK
    decimal RoundISK(decimal val, Rounding mode)
    {
        val = Math.Round(val, 0 + 5, MidpointRounding.AwayFromZero);
        return mode switch
        {
            Rounding.RoundToEven => Math.Round(val, 0, MidpointRounding.ToEven),
            Rounding.AwayFromZero => Math.Round(val, 0, MidpointRounding.AwayFromZero),
            Rounding.RoundDown => Math.Floor(val),
            Rounding.RoundUp => Math.Ceiling(val),
            Rounding.None => val,
            _ => val
        };
    }

    [Theory]
    [InlineData(true, 1538.42, 0.24, "is-IS", 4)]
    [InlineData(false, 1538.42, 0.24, "is-IS", 4)]
    public void Price_Uses_PerUnit_Rounding_Consistently(bool vatIncluded, decimal p, decimal vat, string culture, int qty)
    {
        using var scope = new ConfigurationScope(
            ("Ekom:VatCalcRounding", "RoundToEven"),
            ("Ekom:VatRoundingScope", "PerUnit")
        );

        var currency = new CurrencyModel { CurrencyValue = culture, CurrencyFormat = "C" };
        var iso = currency.ISOCurrencySymbol;

        var sut = new Price(p, currency, vat, vatIncluded, null, qty);

        if (vatIncluded)
        {
            // Per-unit VAT-INCLUDED:
            // 1) unit net = round(p / (1+vat))
            // 2) WithoutVat = unitNet * qty
            // 3) WithVat preserves sticker gross = p * qty
            var unitNet = EkomExpected(p / (1m + vat), iso, Rounding.RoundToEven, 0);
            Assert.Equal(unitNet * qty, sut.WithoutVat.Value);
            Assert.Equal(p * qty, sut.WithVat.Value);
        }
        else
        {
            // Per-unit VAT-EXCLUDED:
            // 1) unit gross = round(p * (1+vat))
            // 2) WithVat = unitGross * qty
            // 3) WithoutVat preserves net = p * qty
            var unitGross = EkomExpected(p * (1m + vat), iso, Rounding.RoundToEven, 0);
            Assert.Equal(unitGross * qty, sut.WithVat.Value);
            Assert.Equal(p * qty, sut.WithoutVat.Value);
        }
    }

    [Theory]
    [InlineData(true, 1538.42, 0.24, "is-IS", 4)]
    [InlineData(false, 1538.42, 0.24, "en-US", 4)]
    public void Basic_Invariants(bool vatIncluded, decimal p, decimal vat, string culture, int qty)
    {
        using var scope = new ConfigurationScope(("Ekom:VatCalcRounding", "RoundToEven"));
        var currency = new CurrencyModel { CurrencyValue = culture, CurrencyFormat = "C" };
        var price = new Price(p, currency, vat, vatIncluded, null, qty);

        var diff = price.WithVat.Value - price.WithoutVat.Value;
        Assert.Equal(diff, price.Vat.Value, precision: 0);

        var price2 = new Price(p, currency, vat, vatIncluded, null, qty + 1);
        Assert.True(price2.WithVat.Value >= price.WithVat.Value);
    }

    [Fact]
    public void ISK_PerUnit_AwayFromZero_Gross1538_Qty4_Vat24()
    {
        using var scope = new ConfigurationScope(
            ("Ekom:VatCalcRounding", "AwayFromZero"),
            ("Ekom:VatRoundingScope", "PerUnit")
        );

        const decimal unitGross = 1538m; // VAT-inclusive price per unit
        const decimal vat = 0.24m;
        const int qty = 4;

        var currency = new CurrencyModel { CurrencyValue = "is-IS", CurrencyFormat = "C" };
        var price = new Price(
            price: unitGross,
            currency: currency,
            vat: vat,
            vatIncludedInPrice: true,
            discount: null,
            quantity: qty,
            discountAlwaysBeforeVat: false
        );

        // 1) Derive unit NET (rounded, ISK 0 decimals) from unit GROSS
        // 2) Line NET = unit NET * qty
        // 3) Line VAT = round(line NET * 24%)
        // 4) Line GROSS = line NET + line VAT
        var lineNet = price.WithoutVat.Value; // should be 1240 * 4 = 4960
        Assert.Equal(4960m, lineNet);

        // Use Calculator to compute VAT from NET (ISK rounding inside)
        var lineVat = Calculator.VatAmountFromWithoutVat(lineNet, vat, currency.ISOCurrencySymbol);
        Assert.Equal(1190m, lineVat);

        var lineGross = lineNet + lineVat;
        Assert.Equal(6150m, lineGross);

        // Guardrail: self-consistency of VAT decomposition
        Assert.Equal(lineGross - lineNet, lineVat);
    }

    [Fact]
    public void ISK_PerUnit_RoundToEven_Gross10_Qty1_Vat24_VatIncluded()
    {
        using var scope = new ConfigurationScope(
            ("Ekom:VatCalcRounding", "RoundToEven"),
            ("Ekom:VatRoundingScope", "PerUnit")
        );

        const decimal unitGross = 10m;   // VAT-inclusive price per unit
        const decimal vat = 0.24m;
        const int qty = 1;

        var currency = new CurrencyModel { CurrencyValue = "is-IS", CurrencyFormat = "C" };
        var price = new Price(
            price: unitGross,
            currency: currency,
            vat: vat,
            vatIncludedInPrice: true,
            discount: null,
            quantity: qty,
            discountAlwaysBeforeVat: false
        );

        // Expectations with PerUnit + RoundToEven:
        // unitNet = round(10 / 1.24) = 8
        // unitVat = 10 - 8 = 2
        // lineNet = 8, lineVat = 2, lineGross = 10

        var lineNet = price.WithoutVat.Value;
        var lineVat = price.Vat.Value;
        var lineGross = price.WithVat.Value;

        Assert.Equal(8m, lineNet);
        Assert.Equal(2m, lineVat);
        Assert.Equal(10m, lineGross);

        // Guardrail: recompute VAT from NET using Calculator (ISK rounding inside)
        var vatFromNet = Calculator.VatAmountFromWithoutVat(lineNet, vat, currency.ISOCurrencySymbol);
        Assert.Equal(2m, vatFromNet);

        // Self-consistency
        Assert.Equal(lineGross - lineNet, lineVat);
    }

    [Fact]
    public void ISK_ZeroVat_PreservesGross()
    {
        using var scope = new ConfigurationScope(
            ("Ekom:VatCalcRounding", "RoundToEven"),
            ("Ekom:VatRoundingScope", "PerUnit")
        );

        const decimal unitPrice = 100m;
        const decimal vat = 0m;
        const int qty = 3;

        var currency = new CurrencyModel { CurrencyValue = "is-IS", CurrencyFormat = "C" };
        var price = new Price(
            price: unitPrice,
            currency: currency,
            vat: vat,
            vatIncludedInPrice: true,
            discount: null,
            quantity: qty
        );

        Assert.Equal(300m, price.WithVat.Value);
        Assert.Equal(300m, price.WithoutVat.Value);
        Assert.Equal(0m, price.Vat.Value);
    }

    [Fact]
    public void USD_PerUnit_VatExcluded_NoExtraRounding()
    {
        using var scope = new ConfigurationScope(
            ("Ekom:VatCalcRounding", "RoundToEven"),
            ("Ekom:VatRoundingScope", "PerUnit")
        );

        const decimal unitNet = 19.99m;
        const decimal vat = 0.10m; // 10% VAT
        const int qty = 2;

        var currency = new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" };
        var price = new Price(
            price: unitNet,
            currency: currency,
            vat: vat,
            vatIncludedInPrice: false,
            discount: null,
            quantity: qty
        );

        // Gross per unit = 19.99 * 1.1 = 21.989
        var expectedUnitGross = 21.989m;
        var expectedGross = expectedUnitGross * qty;

        Assert.Equal(unitNet * qty, price.WithoutVat.Value);
        Assert.Equal(expectedGross, price.WithVat.Value, 3); // allow 3 decimal places
        Assert.Equal(expectedGross - unitNet * qty, price.Vat.Value, 3);
    }

    [Fact]
    public void ISK_PerUnit_vs_PerTotal_ShouldDiffer()
    {
        const decimal unitGross = 1538.42m;
        const decimal vat = 0.24m;
        const int qty = 4;
        var currency = new CurrencyModel { CurrencyValue = "is-IS", CurrencyFormat = "C" };
        var iso = currency.ISOCurrencySymbol;

        // PerUnit
        using (var scope = new ConfigurationScope(
            ("Ekom:VatCalcRounding", "RoundToEven"),
            ("Ekom:VatRoundingScope", "PerUnit")))
        {
            var price = new Price(unitGross, currency, vat, vatIncludedInPrice: true, quantity: qty);

            // expected: unitNetRounded * qty, where unitNetRounded = round(unitGross / (1+vat))
            var unitNetRaw = unitGross / (1m + vat);
            var unitNetRounded = EkomExpected(unitNetRaw, iso, Rounding.RoundToEven, 0);
            var expectedPerUnitNet = unitNetRounded * qty;

            Assert.Equal(expectedPerUnitNet, price.WithoutVat.Value);   // = 1241 * 4 = 4964
        }

        // PerTotal
        using (var scope = new ConfigurationScope(
            ("Ekom:VatCalcRounding", "RoundToEven"),
            ("Ekom:VatRoundingScope", "PerTotal")))
        {
            var price = new Price(unitGross, currency, vat, vatIncludedInPrice: true, quantity: qty);

            // expected: round( (unitGross*qty) / (1+vat) )
            var netTotalRaw = (unitGross * qty) / (1m + vat);
            var expectedPerTotalNet = EkomExpected(netTotalRaw, iso, Rounding.RoundToEven, 0);

            Assert.Equal(expectedPerTotalNet, price.WithoutVat.Value);  // different from 4964

            // sanity: policies differ
            Assert.NotEqual(expectedPerTotalNet,
                            EkomExpected(unitGross / (1m + vat), iso, Rounding.RoundToEven, 0) * qty);
        }
    }
}
