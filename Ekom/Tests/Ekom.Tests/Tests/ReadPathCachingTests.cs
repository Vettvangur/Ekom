using Ekom.Models;
using Ekom.Tests.Objects;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ekom.Tests.Tests;

public class ReadPathCachingTests
{
    [Fact]
    public void Price_ReusesTotalsAndFollowsConfigurationChanges()
    {
        var currency = new CurrencyModel { CurrencyValue = "is-IS", CurrencyFormat = "C" };
        var price = new Price(999m, currency, 0.24m, true, quantity: 3m);

        using (new ConfigurationScope(("Ekom:VatRoundingScope", "PerUnit")))
        {
            Assert.Equal(2418m, price.WithoutVat.Value);
            Assert.Equal(2418m, price.WithoutVat.Value);
            Assert.Equal(2997m, price.WithVat.Value);
        }

        using (new ConfigurationScope(("Ekom:VatRoundingScope", "PerTotal")))
        {
            Assert.Equal(2417m, price.WithoutVat.Value);
        }
    }

    [Fact]
    public void Price_FollowsDiscountAlwaysBeforeVatChanges()
    {
        using var configurationScope = new ConfigurationScope();
        var currency = new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" };
        var discount = new OrderedDiscount(Guid.NewGuid(), "Discount", false, 10m, DiscountType.Fixed, [], [], new Constraints(), false, false);
        var price = new Price(124m, currency, 0.24m, true, discount);

        Assert.Equal(114m, price.AfterDiscount.Value);

        price.DiscountAlwaysBeforeVAT = true;

        Assert.Equal(111.6m, price.AfterDiscount.Value);
    }

    [Fact]
    public void CurrencyModel_ResolvesSymbolsForKnownAndUnknownValues()
    {
        var known = new CurrencyModel { CurrencyValue = "is-IS" };
        var unknown = new CurrencyModel { CurrencyValue = "not-a-region" };

        Assert.Equal("ISK", known.ISOCurrencySymbol);
        Assert.Equal("ISK", known.ISOCurrencySymbol);
        Assert.Equal(string.Empty, unknown.ISOCurrencySymbol);
        Assert.Equal(string.Empty, unknown.CurrencySymbol);
    }

    [Fact]
    public void Store_ParsesCurrencyJsonOnceAndFollowsPropertyChanges()
    {
        using var configurationScope = new ConfigurationScope(addServices: services =>
            services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor()));
        var store = new TestStore();
        store.Set("currency", "[{\"CurrencyValue\":\"is-IS\",\"CurrencyFormat\":\"C\"},{\"CurrencyValue\":\"en-US\",\"CurrencyFormat\":\"C\"}]");

        var first = store.Currencies;

        Assert.Same(first, store.Currencies);
        Assert.Equal(["is-IS", "en-US"], first.Select(x => x.CurrencyValue));
        Assert.Same(first[0], store.Currency);

        store.Set("currency", "en-GB");

        Assert.Equal("en-GB", Assert.Single(store.Currencies).CurrencyValue);
        Assert.Equal("en-GB", store.Currency.CurrencyValue);
    }

    [Fact]
    public void NodeEntity_ReusesParsedValuesAndFollowsPropertyChanges()
    {
        var key = Guid.NewGuid();
        var store = new TestStore();
        store.Set("__Key", key.ToString());
        store.Set("sortOrder", "7");
        store.Set("level", "3");
        store.Set("createDate", "2026-01-02T03:04:05.0000000Z");

        Assert.Same(store.Properties, store.Properties);
        Assert.Equal(key, store.Key);
        Assert.Equal(7, store.SortOrder);
        Assert.Equal(7, store.SortOrder);
        Assert.Equal(3, store.Level);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), store.CreateDate.ToUniversalTime());

        store.Set("sortOrder", "9");
        store.Set("level", "not a number");

        Assert.Equal(9, store.SortOrder);
        Assert.Equal(0, store.Level);

        store.ReplaceProperties(new Dictionary<string, string> { ["sortOrder"] = "11" });

        Assert.Equal(11, store.SortOrder);
        Assert.False(store.Properties.ContainsKey("level"));
    }

    [Fact]
    public void GetPropertyValue_FallsBackToEmptyKeyValue()
    {
        IReadOnlyDictionary<string, string> withEmptyKey = new Dictionary<string, string>
        {
            ["title"] = "",
            [""] = "fallback",
        };
        IReadOnlyDictionary<string, string> withoutEmptyKey = new Dictionary<string, string> { ["title"] = "" };

        Assert.Equal("fallback", withEmptyKey.GetPropertyValue("title"));
        Assert.Equal("fallback", withEmptyKey.GetRawPropertyValue("missing"));
        Assert.Equal(string.Empty, withoutEmptyKey.GetPropertyValue("title"));
        Assert.Equal(string.Empty, withoutEmptyKey.GetRawPropertyValue("missing"));
    }

    private sealed class TestStore : Store
    {
        public override string Alias => "Store";

        public void Set(string alias, string value) => _properties[alias] = value;

        public void ReplaceProperties(Dictionary<string, string> properties) => _properties = properties;
    }
}
