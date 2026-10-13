using Ekom.API;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProductFilterStoreContextCollection
{
    public const string Name = "Product filter store context";
}

[Collection(ProductFilterStoreContextCollection.Name)]
public class ProductFilterStoreContextTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Response_Passes_Explicit_Store_With_Null_Or_Conflicting_Query(bool async, bool withQuery)
    {
        using var configuration = CreateConfiguration();
        var filter = new StoreAwareFilter();
        var query = withQuery ? CreateQuery() : null;
        var category = Mock.Of<ICategory>();
        using var cts = new CancellationTokenSource();

        var response = async
            ? await ProductResponse.CreateAsync([], query, filter, category, "ActualStore", cts.Token)
            : new ProductResponse([], query, filter, category, "ActualStore");

        Assert.Equal("ActualStore", filter.StoreAlias);
        Assert.Same(query, filter.Query);
        Assert.Same(category, filter.Category);
        Assert.Equal(async ? cts.Token : CancellationToken.None, filter.Token);
        Assert.Empty(response.Products);
        if (query is not null)
            Assert.Equal("WrongStore", query.StoreAlias);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Store_Aware_Response_Preserves_Legacy_Async_Overrides(bool async)
    {
        using var configuration = CreateConfiguration();
        var filter = new LegacyFilter();
        var product = Mock.Of<IProduct>();

        var response = async
            ? await ProductResponse.CreateAsync([product], null, filter, null, "ActualStore", CancellationToken.None)
            : new ProductResponse([product], null, filter, null, "ActualStore");

        Assert.Equal(1, filter.Calls);
        Assert.Empty(response.Products);
    }

    [Fact]
    public async Task Store_Aware_Response_Preserves_Existing_Interface_Implementations()
    {
        using var configuration = CreateConfiguration();
        var filter = new LegacyInterfaceFilter();

        await ProductResponse.CreateAsync([], null, filter, null, "ActualStore", CancellationToken.None);

        Assert.Equal(1, filter.AsyncCalls);
        IProductFilterService service = filter;
        service.ApplyFilters([], null, null, "ActualStore");
        Assert.Equal(1, filter.SyncCalls);
    }

    [Fact]
    public void Store_Aware_Sync_Service_Preserves_Legacy_Sync_Overrides()
    {
        var filter = new LegacySyncFilter();

        filter.ApplyFilters([], null, null, "ActualStore");

        Assert.Equal(1, filter.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_Response_Overloads_Still_Call_Legacy_Filters(bool async)
    {
        using var configuration = CreateConfiguration();
        var filter = new LegacyFilter();

        if (async)
            await ProductResponse.CreateAsync([], null, filter);
        else
            _ = new ProductResponse([], null, filter);

        Assert.Equal(1, filter.Calls);
    }

    [Theory]
    [InlineData(false, "all")]
    [InlineData(true, "all")]
    [InlineData(false, "ids")]
    [InlineData(true, "ids")]
    [InlineData(false, "keys")]
    [InlineData(true, "keys")]
    [InlineData(false, "skus")]
    [InlineData(true, "skus")]
    public async Task Explicit_Catalog_Store_Reaches_Filters_Without_Http_Context(bool async, string retrieval)
    {
        using var configuration = CreateConfiguration();
        var filter = new StoreAwareFilter();
        var catalog = CreateCatalog(configuration.Instance, filter);
        var query = CreateQuery();

        if (async)
        {
            _ = retrieval switch
            {
                "all" => await catalog.GetAllProductsAsync("ActualStore", query),
                "ids" => await catalog.GetProductsByIdsAsync("ActualStore", query),
                "keys" => await catalog.GetProductsByKeysAsync("ActualStore", query),
                _ => await catalog.GetProductsBySkusAsync("ActualStore", query),
            };
        }
        else
        {
            _ = retrieval switch
            {
                "all" => catalog.GetAllProducts("ActualStore", query),
                "ids" => catalog.GetProductsByIds("ActualStore", query),
                "keys" => catalog.GetProductsByKeys("ActualStore", query),
                _ => catalog.GetProductsBySkus("ActualStore", query),
            };
        }

        Assert.Equal("ActualStore", filter.StoreAlias);
        Assert.Equal("WrongStore", query.StoreAlias);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_Store_Catalog_Passes_Resolved_Store_With_Null_Query(bool async)
    {
        using var configuration = CreateConfiguration();
        var filter = new StoreAwareFilter();
        var catalog = CreateCatalog(configuration.Instance, filter);

        if (async)
            await catalog.GetAllProductsAsync();
        else
            catalog.GetAllProducts();

        Assert.Equal("ActualStore", filter.StoreAlias);
        Assert.Null(filter.Query);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Category_Retrieval_Passes_Category_Store_Despite_Conflicting_Query(bool async, bool recursive)
    {
        var filter = new StoreAwareFilter();
        using var configuration = new ConfigurationScope(addServices: services =>
        {
            services.AddSingleton(Mock.Of<IMetafieldService>());
            services.AddSingleton<IProductFilterService>(filter);
            services.AddSingleton<IPerStoreIndexedCache<IProduct>>(sp => new ProductCache(
                sp.GetRequiredService<Configuration>(),
                NullLogger<IPerStoreIndexedCache<IProduct>>.Instance,
                Mock.Of<IBaseCache<IStore>>(), Mock.Of<IPerStoreFactory<IProduct>>(), sp));
            services.AddSingleton<IPerStoreIndexedCache<ICategory>>(sp => new CategoryCache(
                sp.GetRequiredService<Configuration>(),
                NullLogger<IPerStoreIndexedCache<ICategory>>.Instance,
                Mock.Of<IBaseCache<IStore>>(), Mock.Of<IPerStoreFactory<ICategory>>(), sp));
        });
        var store = new Mock<IStore>();
        store.SetupGet(x => x.Alias).Returns("CategoryStore");
        var category = new Category(store.Object);
        var query = CreateQuery();

        if (async)
            _ = recursive ? await category.ProductsRecursiveAsync(query) : await category.ProductsAsync(query);
        else
            _ = recursive ? category.ProductsRecursive(query) : category.Products(query);

        Assert.Equal("CategoryStore", filter.StoreAlias);
        Assert.Same(category, filter.Category);
        Assert.Equal("WrongStore", query.StoreAlias);
    }

    [Fact]
    public async Task Legacy_Default_Token_Calls_Remain_Unambiguous()
    {
        using var configuration = CreateConfiguration();
        var filter = new LegacyFilter();
        var product = Mock.Of<IProduct>();

        await ProductResponse.CreateAsync([product], null, filter, null, default);
        await filter.ApplyFiltersAsync([product], null, null, default);

        Assert.Equal(2, filter.Calls);
    }

    [Fact]
    public async Task Store_Aware_Filter_Receives_Products_And_Preserves_Their_Results()
    {
        using var configuration = CreateConfiguration();
        var filter = new StoreAwareFilter();
        var product = Mock.Of<IProduct>();

        var response = await ProductResponse.CreateAsync([product], null, filter, null, "ActualStore", CancellationToken.None);

        Assert.Same(product, Assert.Single(response.Products));
        Assert.Equal("ActualStore", filter.StoreAlias);
    }

    [Fact]
    public async Task Store_Aware_Response_Preserves_RaiseEvents_And_Cancellation_Behavior()
    {
        using var configuration = CreateConfiguration();
        var filter = new StoreAwareFilter();
        var query = CreateQuery();
        query.RaiseEvents = false;

        await ProductResponse.CreateAsync([], query, filter, null, "ActualStore", CancellationToken.None);
        Assert.Null(filter.StoreAlias);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProductResponse.CreateAsync([], null, filter, null, "ActualStore", cts.Token));
        Assert.Null(filter.StoreAlias);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Rejects_Empty_Explicit_Store_Context(string storeAlias)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ProductResponse.CreateAsync([], null, null, null, storeAlias, CancellationToken.None));
        Assert.Throws<ArgumentException>(() => new ProductResponse([], null, null, null, storeAlias));
    }

    private static ProductQuery CreateQuery()
        => new() { StoreAlias = "WrongStore", OrderBy = OrderBy.NoOrder };

    private static ConfigurationScope CreateConfiguration()
        => new(addServices: services => services.AddSingleton(Mock.Of<IMetafieldService>()));

    private static Catalog CreateCatalog(Configuration configuration, IProductFilterService filter)
    {
        var cache = new Mock<IPerStoreIndexedCache<IProduct>>();
        cache.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IProduct>>
        {
            ["ActualStore"] = new(),
        });
        var store = new Mock<IStore>();
        store.SetupGet(x => x.Alias).Returns("ActualStore");
        var stores = new Mock<IStoreService>();
        stores.Setup(x => x.GetStoreFromCache()).Returns(store.Object);
        return new Catalog(
            NullLogger<Catalog>.Instance,
            configuration,
            Configuration.Resolver.GetRequiredService<IServiceScopeFactory>(),
            cache.Object,
            Mock.Of<IPerStoreIndexedCache<ICategory>>(),
            Mock.Of<IPerStoreCache<IProductDiscount>>(),
            Mock.Of<IPerStoreIndexedCache<IVariant>>(),
            Mock.Of<IPerStoreIndexedCache<IVariantGroup>>(),
            stores.Object,
            new HttpContextAccessor(),
            filter);
    }

    private sealed class StoreAwareFilter : ProductFilterService
    {
        public string? StoreAlias { get; private set; }
        public ProductQuery? Query { get; private set; }
        public ICategory? Category { get; private set; }
        public CancellationToken Token { get; private set; }

        public override Task<IEnumerable<IProduct>> ApplyFiltersAsync(
            IEnumerable<IProduct> products, ProductQuery? query, ICategory? category, string storeAlias, CancellationToken ct)
        {
            StoreAlias = storeAlias;
            Query = query;
            Category = category;
            Token = ct;
            return Task.FromResult(products);
        }
    }

    private sealed class LegacyFilter : ProductFilterService
    {
        public int Calls { get; private set; }

        public override Task<IEnumerable<IProduct>> ApplyFiltersAsync(
            IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Enumerable.Empty<IProduct>());
        }
    }

    private sealed class LegacySyncFilter : ProductFilterService
    {
        public int Calls { get; private set; }

        public override IEnumerable<IProduct> ApplyFilters(IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null)
        {
            Calls++;
            return products;
        }
    }

    private sealed class LegacyInterfaceFilter : IProductFilterService
    {
        public int AsyncCalls { get; private set; }
        public int SyncCalls { get; private set; }

        public IEnumerable<IProduct> ApplyFilters(IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null)
        {
            SyncCalls++;
            return products;
        }

        public Task<IEnumerable<IProduct>> ApplyFiltersAsync(
            IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null, CancellationToken ct = default)
        {
            AsyncCalls++;
            return Task.FromResult(products);
        }
    }
}
