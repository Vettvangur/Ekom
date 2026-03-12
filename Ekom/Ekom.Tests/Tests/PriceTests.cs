using Ekom.Models;
using Ekom.Tests.Objects;
using Ekom.Utilities;
using Newtonsoft.Json;
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

        var policies = new[]
        {
            VatIncludedPerUnitPolicy.LineLevelVat,
            VatIncludedPerUnitPolicy.PreserveStickerGross
        };

        foreach (var s in stores)
            foreach (var scope in scopes)
                foreach (var m in modes)
                    foreach (var q in quantities)
                        foreach (var pol in policies)
                            yield return new object[] { s.Name, s.Store, s.Culture, scope, m, q, pol };
    }

    private static CurrencyModel Currency(string culture, string format = "C") =>
        new CurrencyModel { CurrencyValue = culture, CurrencyFormat = format };

    // faithful to Calculator.PerformVatRounding + EkomRounding
    private static decimal EkomExpected(decimal val, string iso, Rounding rounding, int decimals = 0)
    {
        if (!iso.Equals("ISK", StringComparison.OrdinalIgnoreCase))
            return val; // only ISK coerces rounding during calc in our policy

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
        decimal qty,
        VatIncludedPerUnitPolicy policy)
    {
        using var config = new ConfigurationScope(
            ("Ekom:VatCalcRounding", mode.ToString()),
            ("Ekom:VatRoundingScope", scope.ToString()),
            ("Ekom:VatIncludedPerUnitPolicy", policy.ToString())
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

        // ===== 0% VAT explicit handling =====
        if (store.Vat == 0m)
        {
            bool isISK = iso.Equals("ISK", StringComparison.OrdinalIgnoreCase);

            if (store.VatIncludedInPrice)
            {
                if (scope == VatRoundingScope.PerUnit)
                {
                    var expectedUnit = isISK ? EkomExpected(unitPrice, iso, mode, 0) : unitPrice;
                    var expected = expectedUnit * qty;

                    Assert.Equal(expected, withVat);
                    Assert.Equal(expected, withoutVat);
                }
                else // PerTotal
                {
                    var total = unitPrice * qty;
                    var expected = isISK ? EkomExpected(total, iso, mode, 0) : total;

                    Assert.Equal(expected, withVat);
                    Assert.Equal(expected, withoutVat);
                }
            }
            else
            {
                // VAT excluded: same numbers since VAT=0
                if (scope == VatRoundingScope.PerUnit)
                {
                    var expectedUnit = isISK ? EkomExpected(unitPrice, iso, mode, 0) : unitPrice;
                    var expected = expectedUnit * qty;

                    Assert.Equal(expected, withoutVat);
                    Assert.Equal(expected, withVat);
                }
                else // PerTotal
                {
                    var total = unitPrice * qty;
                    var expected = isISK ? EkomExpected(total, iso, mode, 0) : total;

                    Assert.Equal(expected, withoutVat);
                    Assert.Equal(expected, withVat);
                }
            }

            Assert.Equal(0m, price.Vat.Value);
            return; // skip the non-zero VAT logic
        }
        // ===== end 0% VAT =====

        if (store.VatIncludedInPrice)
        {
            if (scope == VatRoundingScope.PerUnit)
            {
                // Compute rounded unit net
                var unitNetRaw = unitPrice / (1m + store.Vat);
                var unitNetRounded = EkomExpected(unitNetRaw, iso, mode, 0);
                var lineNet = unitNetRounded * qty;

                if (policy == VatIncludedPerUnitPolicy.PreserveStickerGross)
                {
                    // Gross is sticker × qty, VAT is residual
                    Assert.Equal(unitPrice * qty, withVat);
                    Assert.Equal(lineNet, withoutVat);
                    Assert.Equal(withVat - withoutVat, price.Vat.Value, 0);
                }
                else // LineLevelVat
                {
                    // VAT recomputed at line level
                    var expectedVat = EkomExpected(lineNet * store.Vat, iso, mode, 0);
                    var expectedGross = lineNet + expectedVat;

                    Assert.Equal(lineNet, withoutVat);
                    Assert.Equal(expectedVat, price.Vat.Value, 0);
                    Assert.Equal(expectedGross, withVat);
                }
            }
            else // PerTotal
            {
                // Round on totals
                var totalGross = unitPrice * qty;
                var netTotalRaw = totalGross / (1m + store.Vat);
                var expectedNet = EkomExpected(netTotalRaw, iso, mode, 0);
                var expectedVat = EkomExpected(expectedNet * store.Vat, iso, mode, 0);
                var expectedGross = expectedNet + expectedVat;

                Assert.Equal(expectedNet, withoutVat);
                Assert.Equal(expectedVat, price.Vat.Value, 0);
                Assert.Equal(expectedGross, withVat);
            }
        }
        else
        {
            // VAT excluded
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
                var grossTotalRaw = (unitPrice * qty) * (1m + store.Vat);
                var expected = EkomExpected(grossTotalRaw, iso, mode, 0);

                Assert.Equal(expected, withVat);
            }
        }

        // Invariants (non-zero VAT)
        var diff = withVat - withoutVat;
        Assert.Equal(diff, price.Vat.Value, 0);
        Assert.InRange(price.Vat.Value, 0m, withVat);
    }


    [Fact]
    public void CurrencyModel_Derives_ISO_From_Culture()
    {
        var isk = new CurrencyModel { CurrencyValue = "is-IS", CurrencyFormat = "C" };
        Assert.Equal("ISK", isk.ISOCurrencySymbol);

        var usd = new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" };
        Assert.Equal("USD", usd.ISOCurrencySymbol);
    }

    [Fact]
    public void CurrencyModel_Invalid_Culture_Does_Not_Break_Json_Serialization()
    {
        var currency = new CurrencyModel
        {
            CurrencyValue = "Ekom.Models.CultureInfoDto",
            CurrencyFormat = "C"
        };

        var json = JsonConvert.SerializeObject(currency);

        Assert.Contains("\"CurrencyValue\":\"Ekom.Models.CultureInfoDto\"", json);
        Assert.Contains("\"CurrencySymbol\":\"\"", json);
        Assert.Contains("\"ISOCurrencySymbol\":\"\"", json);
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
    public void ISK_PerUnit_VatIncluded_LineLevelVat_1538x4_24pct()
    {
        using var scope = new ConfigurationScope(
            ("Ekom:VatCalcRounding", "AwayFromZero"),
            ("Ekom:VatRoundingScope", "PerUnit"),
            ("Ekom:VatIncludedPerUnitPolicy", "LineLevelVat")
        );

        var currency = new CurrencyModel { CurrencyValue = "is-IS", CurrencyFormat = "C" };
        var price = new Price(1538m, currency, 0.24m, vatIncludedInPrice: true, quantity: 4);

        Assert.Equal(4960m, price.WithoutVat.Value); // 1240 × 4
        Assert.Equal(1190m, price.Vat.Value);        // round(4960 × 0.24)
        Assert.Equal(6150m, price.WithVat.Value);    // 4960 + 1190
    }

    [Fact]
    public void ISK_PerUnit_VatIncluded_PreserveSticker_1538x4_24pct()
    {
        using var scope = new ConfigurationScope(
            ("Ekom:VatCalcRounding", "AwayFromZero"),
            ("Ekom:VatRoundingScope", "PerUnit"),
            ("Ekom:VatIncludedPerUnitPolicy", "PreserveStickerGross")
        );

        var currency = new CurrencyModel { CurrencyValue = "is-IS", CurrencyFormat = "C" };
        var price = new Price(1538m, currency, 0.24m, vatIncludedInPrice: true, quantity: 4);

        Assert.Equal(4960m, price.WithoutVat.Value); // 1240 × 4
        Assert.Equal(1192m, price.Vat.Value);        // 298 × 4 per-unit residuals
        Assert.Equal(6152m, price.WithVat.Value);    // sticker × qty
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
