using Ekom.API;
using Ekom.Cache;
using Ekom.Events;
using Ekom.Models;
using Ekom.Models.Umbraco;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NativeDiscountPriceCollection
{
    public const string Name = "Native discount price";
}

[Collection(NativeDiscountPriceCollection.Name)]
public class NativeDiscountPriceTests
{
    [Fact]
    public void GetPriceValues_LegacyShortAndPartialCallsRemainSupported()
    {
        using var fixture = new Fixture();
        const string raw = "[{\"Currency\":\"en-US\",\"Price\":100}]";
        var currencies = fixture.Store.Currencies;
        var currency = fixture.Store.Currency;

        List<IPrice>[] results =
        [
            StringExtension.GetPriceValues(raw, currencies, 0, true),
            raw.GetPriceValues(currencies, 0, true, currency),
            raw.GetPriceValues(currencies, 0, true, currency, "Web"),
            raw.GetPriceValues(currencies, 0, true, currency, "Web", "-1,100,200"),
            raw.GetPriceValues(currencies, 0, true, currency, "Web", "-1,100,200", []),
            raw.GetPriceValues(currencies, 0, true, storeAlias: "Web", path: "-1,100,200"),
        ];

        foreach (var prices in results)
        {
            var price = Assert.Single(prices);
            Assert.Same(currency, price.Currency);
            Assert.Equal(100m, price.AfterDiscount.Value);
            Assert.False(price.HasDiscount);
        }
    }

    [Theory]
    [InlineData("80")]
    [InlineData("[{\"Currency\":\"en-US\",\"Price\":80},{\"Currency\":\"en-GB\",\"Price\":70}]")]
    public async Task ProductDiscount_ExplicitCurrencyUsesConfiguredDefaultForScalarBase(string target)
    {
        using var fixture = new Fixture(useSecondCurrency: true);
        var product = fixture.Product("100", target);
        var usd = fixture.Store.Currencies[0];
        var gbp = fixture.Store.Currency;

        var sync = product.ProductDiscount(null, usd);
        var asyncDiscount = await product.ProductDiscountAsync(null, CancellationToken.None, usd);

        Assert.NotNull(sync);
        Assert.Equal(20m, sync.Amount);
        Assert.Equal(sync.Amount, asyncDiscount?.Amount);
        Assert.Null(product.ProductDiscount(null, gbp));
        Assert.Null(await product.ProductDiscountAsync(null, CancellationToken.None, gbp));
    }

    [Fact]
    public async Task Variant_ExplicitCurrencyUsesConfiguredDefaultForScalarBaseAndParentFallback()
    {
        using var fixture = new Fixture(useSecondCurrency: true);
        var product = fixture.Product("[{\"Currency\":\"en-US\",\"Price\":100},{\"Currency\":\"en-GB\",\"Price\":200}]",
            "[{\"Currency\":\"en-US\",\"Price\":80},{\"Currency\":\"en-GB\",\"Price\":150}]");
        var variant = new TestVariant(Content("120",
            "[{\"Currency\":\"en-US\",\"Price\":90},{\"Currency\":\"en-GB\",\"Price\":50}]",
            "-1,100,200,300,400"), fixture.Store, product);
        var usd = fixture.Store.Currencies[0];
        var gbp = fixture.Store.Currency;

        Assert.Equal(30m, variant.ProductDiscount("120", usd)?.Amount);
        Assert.Equal(30m, (await variant.ProductDiscountAsync("120", CancellationToken.None, usd))?.Amount);
        Assert.Equal(50m, variant.ProductDiscount("200", gbp)?.Amount);
        Assert.Equal(50m, (await variant.ProductDiscountAsync("200", CancellationToken.None, gbp))?.Amount);
    }

    [Fact]
    public void ScalarBuilders_PreserveExplicitFallbackCurrency()
    {
        using var fixture = new Fixture(useSecondCurrency: true);
        var currency = fixture.Store.Currency;
        var built = PriceBuilder.BuildPricesSync("100", fixture.Store.Currencies, 0, true,
            currency, "Web", "-1,100,200", []);
        var extended = "100".GetPriceValues(fixture.Store.Currencies, 0, true, currency);

        foreach (var prices in new[] { built, extended })
        {
            var price = Assert.Single(prices);
            Assert.Same(currency, price.Currency);
            Assert.Equal(100m, price.AfterDiscount.Value);
        }
    }

    [Theory]
    [InlineData("80", "Web", "en-US", 80)]
    [InlineData("80,5", "Web", "en-US", 80.5)]
    [InlineData("80", "Web", "en-GB", 100)]
    [InlineData("[{\"Currency\":\"en-GB\",\"Price\":70},{\"currency\":\"EN-us\",\"price\":\"80\"}]", "Web", "en-US", 80)]
    [InlineData("[{\"Currency\":\"en-GB\",\"Price\":70}]", "Web", "en-US", 100)]
    [InlineData("{\"values\":{\"web\":[{\"Currency\":\"en-US\",\"Price\":80}],\"Other\":[{\"Currency\":\"en-US\",\"Price\":60}]}}", "Web", "en-US", 80)]
    [InlineData("{\"Web\":80,\"Other\":60}", "Other", "en-US", 60)]
    [InlineData("{\"Other\":60}", "Web", "en-US", 100)]
    [InlineData("", "Web", "en-US", 100)]
    [InlineData("not a price", "Web", "en-US", 100)]
    [InlineData("[{", "Web", "en-US", 100)]
    [InlineData("[{\"Currency\":\"en-US\",\"Price\":{}}]", "Web", "en-US", 100)]
    [InlineData("0", "Web", "en-US", 100)]
    [InlineData("-10", "Web", "en-US", 100)]
    [InlineData("100", "Web", "en-US", 100)]
    [InlineData("120", "Web", "en-US", 100)]
    public void BuildPrices_AppliesOnlyValidTargetForMatchingStoreAndCurrency(
        string target, string storeAlias, string currency, decimal expected)
    {
        using var fixture = new Fixture();
        string raw = $"[{{\"Currency\":\"{currency}\",\"Price\":100}}]";

        var prices = PriceBuilder.BuildPricesSync(raw, fixture.Store.Currencies, 0, true,
            fixture.Store.Currency, storeAlias, "-1,100,200", [], target);
        var price = Assert.Single(prices, x => x.Currency.CurrencyValue == currency);

        Assert.Equal(100m, price.OriginalValue);
        Assert.Equal(expected, price.AfterDiscount.Value);
        Assert.Equal(expected < 100, price.HasDiscount);
        if (expected < 100)
        {
            Assert.Equal(DiscountType.Fixed, price.Discount.Type);
            Assert.Equal(100 - expected, price.Discount.Amount);
        }
        else
        {
            Assert.Null(price.Discount);
        }
    }

    [Theory]
    [InlineData(DiscountType.Fixed, 10, false, 80)]
    [InlineData(DiscountType.Fixed, 30, false, 70)]
    [InlineData(DiscountType.Percentage, 0.1, false, 80)]
    [InlineData(DiscountType.Percentage, 0.3, false, 70)]
    [InlineData(DiscountType.Percentage, 0.1, true, 90)]
    public async Task ProductDiscount_EventSeesAndCanRemoveNativeCandidateBeforeBestSelection(
        DiscountType customerType, decimal customerAmount, bool removeNative, decimal expected)
    {
        using var fixture = new Fixture();
        var product = fixture.Product("100", "80");
        var customer = new Mock<IProductDiscount>();
        customer.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        customer.SetupGet(x => x.Type).Returns(customerType);
        customer.SetupGet(x => x.Amount).Returns(customerAmount);
        customer.SetupGet(x => x.Constraints).Returns((IConstraints)null!);
        int calls = 0;
        Guid nativeKey = Guid.Empty;
        fixture.Events.AfterApplicableDiscountsAsync += (_, args) =>
        {
            calls++;
            Assert.Equal(product.Path, args.Path);
            Assert.Equal("Web", args.StoreAlias);
            Assert.Equal(100m, args.Price);
            var native = Assert.Single(args.ApplicableDiscounts);
            Assert.Equal(DiscountType.Fixed, native.Type);
            Assert.Equal(20m, native.Amount);
            nativeKey = native.Key;
            if (removeNative) args.ApplicableDiscounts.Remove(native);
            args.ApplicableDiscounts.Add(customer.Object);
            return Task.CompletedTask;
        };

        var price = Assert.Single(product.Prices);
        var asyncDiscount = await product.ProductDiscountAsync(null, CancellationToken.None, fixture.Store.Currency);

        Assert.Equal(2, calls);
        Assert.Equal(expected, price.AfterDiscount.Value);
        Assert.NotNull(asyncDiscount);
        var expectedKey = expected == 80 ? nativeKey : customer.Object.Key;
        Assert.Equal(expectedKey, price.Discount.Key);
        Assert.Equal(expectedKey, asyncDiscount.Key);
    }

    [Theory]
    [InlineData(true, 120, 96, 80, 16, 96)]
    [InlineData(false, 100, 80, 80, 16, 96)]
    public void Product_PreservesOriginalPriceAndAppliesTargetInStoreVatBasis(
        bool vatIncluded, decimal original, decimal target, decimal net, decimal vat, decimal gross)
    {
        using var fixture = new Fixture(vatIncluded, 0.2m);
        var product = fixture.Product(original.ToString(System.Globalization.CultureInfo.InvariantCulture),
            target.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var price = Assert.Single(product.Prices);

        Assert.Equal(original, product.OriginalPrice.OriginalValue);
        Assert.False(product.OriginalPrice.HasDiscount);
        Assert.Equal(original, price.OriginalValue);
        Assert.Equal(original, price.BeforeDiscount.Value);
        Assert.Equal(target, price.AfterDiscount.Value);
        Assert.Equal(net, price.WithoutVat.Value);
        Assert.Equal(vat, price.Vat.Value);
        Assert.Equal(gross, price.WithVat.Value);
    }

    [Theory]
    [InlineData("", "50", 100, 80)]
    [InlineData("0", "50", 100, 80)]
    [InlineData("120", "90", 120, 90)]
    [InlineData("120", "", 120, 120)]
    public async Task Variant_InheritsParentTargetOnlyWhenBasePriceIsInherited(
        string ownPrice, string ownTarget, decimal original, decimal expected)
    {
        using var fixture = new Fixture();
        var product = fixture.Product("100", "80");
        var variant = new TestVariant(Content(ownPrice, ownTarget, "-1,100,200,300,400"), fixture.Store, product);

        var price = Assert.Single(variant.Prices);
        var discount = await variant.ProductDiscountAsync(original.ToString(System.Globalization.CultureInfo.InvariantCulture),
            CancellationToken.None, fixture.Store.Currency);

        Assert.Equal(original, price.OriginalValue);
        Assert.Equal(expected, price.AfterDiscount.Value);
        Assert.Equal(expected < original, price.HasDiscount);
        Assert.Equal(price.Discount?.Amount, discount?.Amount);
    }

    [Fact]
    public void Variant_InheritsMissingCurrencyWithoutOverwritingOwnCurrencyTarget()
    {
        using var fixture = new Fixture();
        var product = fixture.Product("[{\"Currency\":\"en-US\",\"Price\":100},{\"Currency\":\"en-GB\",\"Price\":200}]",
            "[{\"Currency\":\"en-US\",\"Price\":80},{\"Currency\":\"en-GB\",\"Price\":150}]");
        var variant = new TestVariant(Content("[{\"Currency\":\"en-US\",\"Price\":120}]",
            "[{\"Currency\":\"en-US\",\"Price\":90},{\"Currency\":\"en-GB\",\"Price\":50}]",
            "-1,100,200,300,400"), fixture.Store, product);

        var prices = variant.Prices;

        Assert.Equal(90m, Assert.Single(prices, x => x.Currency.CurrencyValue == "en-US").AfterDiscount.Value);
        var inherited = Assert.Single(prices, x => x.Currency.CurrencyValue == "en-GB");
        Assert.Equal(200m, inherited.OriginalValue);
        Assert.Equal(150m, inherited.AfterDiscount.Value);
    }

    [Fact]
    public void Prices_CacheTracksOwnAndInheritedTargetChanges()
    {
        using var fixture = new Fixture();
        var product = fixture.Product("100", "80");
        var inherited = new TestVariant(Content("0", "50", "-1,100,200,300,400"), fixture.Store, product);
        var owned = new TestVariant(Content("120", "90", "-1,100,200,300,401"), fixture.Store, product);
        var productPrices = product.Prices;
        var inheritedPrices = inherited.Prices;
        var ownedPrices = owned.Prices;
        Assert.Same(productPrices, product.Prices);
        Assert.Same(inheritedPrices, inherited.Prices);
        Assert.Same(ownedPrices, owned.Prices);
        Assert.Equal(80m, Assert.Single(inheritedPrices).AfterDiscount.Value);

        product.SetTarget("70");
        owned.SetTarget("85");

        Assert.NotSame(productPrices, product.Prices);
        Assert.NotSame(inheritedPrices, inherited.Prices);
        Assert.NotSame(ownedPrices, owned.Prices);
        Assert.Equal(70m, Assert.Single(product.Prices).AfterDiscount.Value);
        Assert.Equal(70m, Assert.Single(inherited.Prices).AfterDiscount.Value);
        Assert.Equal(85m, Assert.Single(owned.Prices).AfterDiscount.Value);
        Assert.Equal(80m, Assert.Single(productPrices).AfterDiscount.Value);
    }

    private static UmbracoContent Content(string price, string target, string path = "-1,100,200")
        => new(new Dictionary<string, string>(), new Dictionary<string, string>
        {
            ["id"] = path.Split(',')[^1],
            ["__Key"] = Guid.NewGuid().ToString(),
            ["parentID"] = "100",
            ["parentKey"] = Guid.NewGuid().ToString(),
            ["level"] = "3",
            ["nodeName"] = "Regression product",
            ["__Path"] = path,
            ["__NodeTypeAlias"] = "ekmProduct",
            ["sortOrder"] = "0",
            ["createDate"] = "2024-01-01T00:00:00Z",
            ["updateDate"] = "2024-01-01T00:00:00Z",
            ["sku"] = "native-price-test",
            ["price"] = price,
            ["ekmDiscountPrice"] = target,
        });

    private sealed class TestVariant(UmbracoContent content, IStore store, IProduct product) : Variant(content, store)
    {
        public override IProduct? Product => product;
        public void SetTarget(string target) => _properties["ekmDiscountPrice"] = target;
    }

    private sealed class TestProduct(UmbracoContent content, IStore store) : Product(content, store)
    {
        public void SetTarget(string target) => _properties["ekmDiscountPrice"] = target;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ConfigurationScope _configuration;
        private readonly MemoryCache _memory = new(new MemoryCacheOptions());
        public DiscountEvents Events { get; } = new();
        public IStore Store { get; }

        public Fixture(bool vatIncluded = true, decimal vat = 0, bool useSecondCurrency = false)
        {
            var usd = new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" };
            var gbp = new CurrencyModel { CurrencyValue = "en-GB", CurrencyFormat = "C" };
            var store = new Mock<IStore>();
            store.SetupGet(x => x.Alias).Returns("Web");
            store.SetupGet(x => x.Currency).Returns(useSecondCurrency ? gbp : usd);
            store.SetupGet(x => x.Currencies).Returns([usd, gbp]);
            store.SetupGet(x => x.Vat).Returns(vat);
            store.SetupGet(x => x.VatIncludedInPrice).Returns(vatIncluded);
            Store = store.Object;
            var discounts = new Mock<IPerStoreCache<IProductDiscount>>();
            discounts.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IProductDiscount>>());
            var stores = new Mock<IStoreService>();
            stores.Setup(x => x.GetStoreByAlias("Web")).Returns(Store);
            stores.Setup(x => x.GetStoreFromCache()).Returns(Store);
            _configuration = new ConfigurationScope(addServices: services =>
            {
                services.AddSingleton<IMemoryCache>(_memory);
                services.AddSingleton(new ProductDiscountService(discounts.Object, Events));
                services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                services.AddSingleton<IStoreService>(stores.Object);
                services.AddTransient(sp => new Catalog(Mock.Of<ILogger<Catalog>>(),
                    sp.GetRequiredService<Configuration>(), sp.GetRequiredService<IServiceScopeFactory>(),
                    Mock.Of<IPerStoreIndexedCache<IProduct>>(), Mock.Of<IPerStoreIndexedCache<ICategory>>(),
                    discounts.Object, Mock.Of<IPerStoreIndexedCache<IVariant>>(),
                    Mock.Of<IPerStoreIndexedCache<IVariantGroup>>(), stores.Object,
                    sp.GetRequiredService<IHttpContextAccessor>(), Mock.Of<IProductFilterService>()));
            });
        }

        public TestProduct Product(string price, string target) => new(Content(price, target), Store);

        public void Dispose()
        {
            _configuration.Dispose();
            _memory.Dispose();
        }
    }
}
