using Ekom.API;
using Ekom.Cache;
using Ekom.Events;
using Ekom.Factories;
using Ekom.Models;
using Ekom.Models.Umbraco;
using Ekom.Services;
using Ekom.Tests.Objects;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProductCategoryHydrationCollection
{
    public const string Name = "Product category hydration";
}

[Collection(ProductCategoryHydrationCollection.Name)]
public class ProductCategoryHydrationTests
{
    [Fact]
    public void ProductConstruction_DoesNotLetPublicCategoryFilteringRemoveStructuralCategories()
    {
        var store = CreateStore();
        var parentCategory = CreateCategory(39080, "/verslun/fyrir-gludyrin/", store);
        var extraCategory = CreateCategory(39081, "/verslun/aukaflokkur/", store);
        var categoryCache = CreateCategoryCache(store.Alias, parentCategory, extraCategory);

        using var config = CreateConfigurationScope(store, categoryCache);

        ValueTask FilterCategories(CategoryEventArgs args, CancellationToken _)
        {
            if (args.Category?.Id is 39080 or 39081)
                args.Category = null;

            return ValueTask.CompletedTask;
        }

        CatalogEvents.BeforeReturnCategoryAsync += FilterCategories;
        try
        {
            var product = new ProductFactory().Create(CreateProductContent(), store);

            Assert.Contains(product.Categories, x => x.Id == parentCategory.Id);
            Assert.Contains(product.Categories, x => x.Id == extraCategory.Id);
            Assert.Contains(product.CategoryAncestors, x => x.Id == parentCategory.Id);
            Assert.Equal("/verslun/fyrir-gludyrin/test-product/", product.Url);
            Assert.Contains(product.UrlsWithContext, x => x.Url == "/verslun/fyrir-gludyrin/test-product/");
            Assert.Null(Catalog.Instance.GetCategory(parentCategory.Id, store.Alias));
            Assert.Same(parentCategory, Catalog.Instance.GetCategory(parentCategory.Id, store.Alias, raiseEvent: false));
        }
        finally
        {
            CatalogEvents.BeforeReturnCategoryAsync -= FilterCategories;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OrderedProductUrl_BypassesVisibilityFiltersButNormalLookupStillFilters(bool exists)
    {
        var store = CreateStore();
        var key = Guid.NewGuid();
        var product = new Mock<IProduct>();
        product.SetupGet(x => x.Url).Returns("/verslun/previously-purchased/");
        var cache = new Mock<IPerStoreIndexedCache<IProduct>>();
        IProduct cachedProduct = product.Object;
        cache.Setup(x => x.TryGetByKey(store.Alias, key, out cachedProduct)).Returns(exists);
        using var config = CreateConfigurationScope(store, CreateCategoryCache(store.Alias), cache.Object);
        var storeInfo = new StoreInfo(Guid.NewGuid(), store.Currency, [store.Currency], "is-IS", store.Alias, true, 0.24m, true);
        var orderedProduct = new OrderedProduct(
            "{\"Properties\":{\"__Key\":\"" + key + "\"}}", storeInfo);
        var filterCalls = 0;
        ValueTask FilterProducts(ProductEventArgs args, CancellationToken _)
        {
            filterCalls++;
            args.Product = null;
            return ValueTask.CompletedTask;
        }

        CatalogEvents.BeforeReturnProductAsync += FilterProducts;
        try
        {
            Assert.Equal(exists ? "/verslun/previously-purchased/" : "", orderedProduct.Url);
            Assert.Equal(0, filterCalls);
            Assert.Null(Catalog.Instance.GetProduct(key, store.Alias));
            Assert.Equal(exists ? 1 : 0, filterCalls);
        }
        finally
        {
            CatalogEvents.BeforeReturnProductAsync -= FilterProducts;
        }
    }

    private static ConfigurationScope CreateConfigurationScope(
        IStore store,
        IPerStoreIndexedCache<ICategory> categoryCache,
        IPerStoreIndexedCache<IProduct>? productCache = null)
        => new(addServices: services =>
        {
            var storeService = new Mock<IStoreService>();
            storeService.Setup(x => x.GetStoreByAlias(store.Alias)).Returns(store);
            storeService.Setup(x => x.GetStoreFromCache()).Returns(store);
            storeService.Setup(x => x.GetAllStores()).Returns(new[] { store });

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IStoreService>(storeService.Object);
            services.AddSingleton<IUrlService>(new TestUrlService());
            services.AddSingleton(categoryCache);
            services.AddSingleton(productCache ?? Mock.Of<IPerStoreIndexedCache<IProduct>>());
            services.AddSingleton(Mock.Of<IPerStoreCache<IProductDiscount>>());
            services.AddSingleton(Mock.Of<IPerStoreIndexedCache<IVariant>>());
            services.AddSingleton(Mock.Of<IPerStoreIndexedCache<IVariantGroup>>());
            services.AddTransient<Catalog>(sp => new Catalog(
                Mock.Of<Microsoft.Extensions.Logging.ILogger<Catalog>>(),
                sp.GetRequiredService<Configuration>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IPerStoreIndexedCache<IProduct>>(),
                sp.GetRequiredService<IPerStoreIndexedCache<ICategory>>(),
                sp.GetRequiredService<IPerStoreCache<IProductDiscount>>(),
                sp.GetRequiredService<IPerStoreIndexedCache<IVariant>>(),
                sp.GetRequiredService<IPerStoreIndexedCache<IVariantGroup>>(),
                sp.GetRequiredService<IStoreService>(),
                sp.GetRequiredService<IHttpContextAccessor>(),
                Mock.Of<IProductFilterService>()));
        });

    private static IPerStoreIndexedCache<ICategory> CreateCategoryCache(
        string storeAlias,
        params ICategory[] categories)
    {
        var categoryCache = new Mock<IPerStoreIndexedCache<ICategory>>();

        foreach (var category in categories)
        {
            var outCategory = category;
            categoryCache
                .Setup(x => x.TryGetById(storeAlias, category.Id, out outCategory))
                .Returns(true);
        }

        return categoryCache.Object;
    }

    private static IStore CreateStore()
    {
        var currency = new CurrencyModel { CurrencyValue = "is-IS", CurrencyFormat = "C" };
        var store = new Mock<IStore>();

        store.SetupGet(x => x.Alias).Returns("Web");
        store.SetupGet(x => x.Currency).Returns(currency);
        store.SetupGet(x => x.Currencies).Returns([currency]);
        store.SetupGet(x => x.Vat).Returns(0.24m);
        store.SetupGet(x => x.VatIncludedInPrice).Returns(true);

        return store.Object;
    }

    private static ICategory CreateCategory(int id, string url, IStore store)
    {
        var category = new Mock<ICategory>();

        category.SetupGet(x => x.Id).Returns(id);
        category.SetupGet(x => x.Store).Returns(store);
        category.SetupGet(x => x.Url).Returns(url);
        category.SetupGet(x => x.VirtualUrl).Returns(false);

        return category.Object;
    }

    private static UmbracoContent CreateProductContent()
        => new(new Dictionary<string, string>(), new Dictionary<string, string>
        {
            ["id"] = "10172728",
            ["__Key"] = "d201ed8f-2964-43de-8ddb-a1dfcea7bdee",
            ["parentID"] = "39080",
            ["parentKey"] = "3b24f567-5dcd-4543-a3e5-47ee77ff0889",
            ["level"] = "4",
            ["nodeName"] = "Test product",
            ["__Path"] = "-1,1000,39080,10172728",
            ["__NodeTypeAlias"] = "ekmProduct",
            ["sortOrder"] = "0",
            ["createDate"] = "2024-01-01T00:00:00Z",
            ["updateDate"] = "2024-01-01T00:00:00Z",
            ["sku"] = "10172728",
            ["slug"] = "test-product",
            ["categories"] = "39081",
        });

    private sealed class TestUrlService : IUrlService
    {
        public List<UmbracoUrl> BuildCategoryUrls(IEnumerable<UmbracoContent> items, IStore store) => [];

        public IEnumerable<string> BuildCategoryUrls(string slug, List<string> hierarchy, IStore store) => [];

        [Obsolete]
        public IEnumerable<string> BuildProductUrls(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId)
            => BuildProductUrlsWithContext(item, categories, store, nodeId).Select(x => x.Url);

        public List<UmbracoUrl> BuildProductUrlsWithContext(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId)
        {
            var category = categories.FirstOrDefault();
            return category == null
                ? []
                :
                [
                    new UmbracoUrl
                    {
                        Store = store.Alias,
                        Culture = "is-IS",
                        Url = $"{category.Url.TrimEnd('/')}/{item.GetValue("slug")}/",
                    },
                ];
        }

        public string? GetNodeEntityUrl(INodeEntityWithUrl node) => node.Urls.FirstOrDefault();
    }
}
