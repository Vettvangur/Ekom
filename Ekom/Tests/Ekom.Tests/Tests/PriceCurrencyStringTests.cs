using Ekom.Events;
using Ekom.Models;
using System.Globalization;
using Xunit;

namespace Ekom.Tests.Tests;

public class PriceCurrencyStringTests
{
    private const decimal Sentinel = 987654.32m;

    [Theory]
    [InlineData("en-US", "C")]
    [InlineData("en-GB", "C2")]
    [InlineData("da-DK", "N0")]
    public void CurrencyString_FormatsWithCurrencyCulture(string culture, string format)
    {
        var price = Create(1234.5m, culture, format);

        Assert.Equal(1234.5m.ToString(format, new CultureInfo(culture)), price.CurrencyString);
    }

    [Fact]
    public void CurrencyString_UsesKrSymbolForIcelandic()
    {
        var price = Create(1990m, "is-IS", "C");

        Assert.Equal(1990m.ToString("C", Configuration.IsCultureInfo), price.CurrencyString);
        Assert.Contains("kr", price.CurrencyString);
    }

    [Fact]
    public void CurrencyString_IsStableAcrossRepeatedAndConcurrentAccess()
    {
        var expected = 1234.5m.ToString("C", new CultureInfo("en-US"));

        Parallel.For(0, 2000, _ => Assert.Equal(expected, Create(1234.5m, "en-US", "C").CurrencyString));
    }

    [Fact]
    public void CurrencyStringFormat_HandlerGetsOwnMutableCultureAndCanOverrideValue()
    {
        var before = Create(Sentinel, "en-US", "C").CurrencyString;
        CultureInfo? received = null;

        // Other tests format prices in parallel, so only react to this test's value.
        void Handler(object? sender, CurrencyStringEventArgs args)
        {
            if (args.Value != Sentinel) return;
            received = args.CultureInfo;
            args.CultureInfo.NumberFormat.CurrencySymbol = "XX";
            args.ValueString = args.Value.ToString("C", args.CultureInfo);
        }

        CatalogEvents.CurrencyStringFormat += Handler;
        try
        {
            var overridden = Create(Sentinel, "en-US", "C").CurrencyString;

            Assert.NotNull(received);
            Assert.False(received!.IsReadOnly);
            Assert.Contains("XX", overridden);
        }
        finally
        {
            CatalogEvents.CurrencyStringFormat -= Handler;
        }

        // The handler's mutation must not leak into later formatting.
        Assert.Equal(before, Create(Sentinel, "en-US", "C").CurrencyString);
    }

    private static CalculatedPrice Create(decimal value, string culture, string format)
        => new(value, new CurrencyModel { CurrencyValue = culture, CurrencyFormat = format });
}
