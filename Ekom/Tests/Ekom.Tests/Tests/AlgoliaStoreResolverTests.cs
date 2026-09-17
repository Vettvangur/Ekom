using Ekom.Algolia;
using Ekom.Algolia.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaStoreResolverTests
{
    [Fact]
    public void Uses_Global_Indexing_When_Store_Has_No_Override()
    {
        var globalIndexing = new AlgoliaIndexingOptions
        {
            Enabled = false,
            Products = false,
            Categories = true,
            BatchSize = 250,
            ProductProperties = ["globalProperty"],
        };
        var resolver = CreateResolver(new AlgoliaOptions
        {
            ApplicationId = "app",
            AdminApiKey = "admin",
            SearchApiKey = "search",
            Indexing = globalIndexing,
            Stores = [new AlgoliaStoreOptions { Alias = "Store" }],
        });

        var store = resolver.Resolve("store");

        Assert.Same(globalIndexing, store.Indexing);
        Assert.False(store.HasIndexingOverride);
    }

    [Fact]
    public void Store_Indexing_Completely_Replaces_Global_Indexing()
    {
        var dispatching = new AlgoliaDispatcherOptions { MaxConcurrency = 7 };
        var resolver = CreateResolver(new AlgoliaOptions
        {
            ApplicationId = "app",
            AdminApiKey = "admin",
            SearchApiKey = "search",
            Indexing = new AlgoliaIndexingOptions
            {
                Enabled = false,
                Products = false,
                Categories = false,
                Variants = true,
                BatchSize = 250,
                ProductProperties = ["globalProperty"],
                ProductCustomRanking = ["desc(globalProductRank)"],
                CategoryCustomRanking = ["asc(globalCategoryRank)"],
                Dispatching = dispatching,
            },
            Stores =
            [
                new AlgoliaStoreOptions
                {
                    Alias = "Store",
                    Indexing = new AlgoliaStoreIndexingOptions
                    {
                        Enabled = true,
                        Products = true,
                        Categories = true,
                        ProductProperties = ["storeProperty"],
                        ProductCustomRanking = ["desc(storeProductRank)"],
                        CategoryCustomRanking = ["asc(storeCategoryRank)"],
                    },
                },
            ],
        });

        var store = resolver.Resolve("STORE");

        Assert.True(store.HasIndexingOverride);
        Assert.True(store.Indexing.Enabled);
        Assert.True(store.Indexing.Products);
        Assert.True(store.Indexing.Categories);
        Assert.False(store.Indexing.Variants);
        Assert.Equal(1000, store.Indexing.BatchSize);
        Assert.Equal(["storeProperty"], store.Indexing.ProductProperties);
        Assert.Equal(["desc(storeProductRank)"], store.Indexing.ProductCustomRanking);
        Assert.Equal(["asc(storeCategoryRank)"], store.Indexing.CategoryCustomRanking);
        Assert.Empty(store.Indexing.FacetAttributes);
        Assert.Empty(store.Indexing.SortedReplicas);
        Assert.Same(dispatching, store.Indexing.Dispatching);
    }

    private static AlgoliaStoreResolver CreateResolver(AlgoliaOptions options)
    {
        return new AlgoliaStoreResolver(
            Options.Create(options),
            Mock.Of<IServiceProvider>(),
            NullLogger<AlgoliaStoreResolver>.Instance);
    }
}
