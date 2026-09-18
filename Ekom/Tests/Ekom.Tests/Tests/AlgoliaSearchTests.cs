using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Ekom.Algolia;
using Ekom.Algolia.Models.Search;
using Ekom.Algolia.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaSearchTests
{
    [Fact]
    public void Binds_Algolia_Search_Options()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ekom:Algolia:ApplicationId"] = "app-id",
                ["Ekom:Algolia:AdminApiKey"] = "admin-key",
                ["Ekom:Algolia:SearchApiKey"] = "search-key",
                ["Ekom:Algolia:Replacement:MaxRetries"] = "400",
                ["Ekom:Algolia:Transformation:MaxBatchSize"] = "200",
                ["Ekom:Algolia:Transformation:MaxAttempts"] = "4",
                ["Ekom:Algolia:Transformation:RetryBaseDelayMilliseconds"] = "500",
                ["Ekom:Algolia:Transformation:EnableSdkLogging"] = "true",
                ["Ekom:Algolia:Search:Enabled"] = "true",
                ["Ekom:Algolia:Search:Products"] = "true",
                ["Ekom:Algolia:Search:Categories"] = "true",
                ["Ekom:Algolia:Search:QuerySuggestions"] = "true",
                ["Ekom:Algolia:Search:IncludeUserToken"] = "true",
                ["Ekom:Algolia:Search:VaryCacheByUserToken"] = "true",
                ["Ekom:Algolia:Search:MinimumQueryLength"] = "3",
                ["Ekom:Algolia:Search:MaxHitsPerPage"] = "50",
                ["Ekom:Algolia:Search:Cache:Enabled"] = "true",
                ["Ekom:Algolia:Search:Cache:DurationMinutes"] = "15",
                ["Ekom:Algolia:Search:Cache:CacheEmptyResults"] = "false",
                ["Ekom:Algolia:Indexing:AttributesForFaceting:0"] = "filterOnly(categoryPageId)",
                ["Ekom:Algolia:Indexing:ProductCustomRanking:0"] = "desc(ProductRanking)",
                ["Ekom:Algolia:Indexing:CategoryCustomRanking:0"] = "asc(SortOrder)",
                ["Ekom:Algolia:Indexing:FacetAttributes:0"] = "metafield:material",
                ["Ekom:Algolia:Indexing:VariantFacetAttributes:color"] = "variantGroup:title",
                ["Ekom:Algolia:Indexing:VariantFacetAttributes:size"] = "variant:title",
                ["Ekom:Algolia:Stores:0:Alias"] = "Store",
                ["Ekom:Algolia:Stores:0:EnableAvailabilityUpdates"] = "true",
                ["Ekom:Algolia:Stores:0:LanguageSettings:QueryLanguages:0"] = "is",
                ["Ekom:Algolia:Stores:0:LanguageSettings:IndexLanguages:0"] = "is",
                ["Ekom:Algolia:Stores:0:LanguageSettings:RemoveStopWords"] = "true",
                ["Ekom:Algolia:Stores:0:LanguageSettings:IgnorePlurals"] = "true",
                ["Ekom:Algolia:Stores:0:LanguageSettings:IgnorePluralsLanguages:0"] = "is",
                ["Ekom:Algolia:ContentIndexing:Enabled"] = "true",
                ["Ekom:Algolia:ContentIndexing:OversizedRecords:Behavior"] = "Skip",
                ["Ekom:Algolia:ContentIndexing:OversizedRecords:MaxSizeBytes"] = "90000",
                ["Ekom:Algolia:ContentIndexing:Indexes:0:IndexName"] = "SearchIndex",
                ["Ekom:Algolia:ContentIndexing:Indexes:0:CustomRanking:0"] = "desc(PublishedDate)",
                ["Ekom:Algolia:ContentIndexing:Indexes:0:ContentTypes:0:Alias"] = "article",
                ["Ekom:Algolia:ContentIndexing:Indexes:0:ContentTypes:0:Properties:0"] = "title",
                ["Ekom:Algolia:ContentIndexing:Indexes:0:ContentTypes:0:Properties:1"] = "publishedAt|unix"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<AlgoliaOptions>().Bind(config.GetSection("Ekom:Algolia"));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AlgoliaOptions>>().Value;

        Assert.Equal("search-key", options.SearchApiKey);
        Assert.Equal(400, options.Replacement.MaxRetries);
        Assert.Equal(200, options.Transformation.MaxBatchSize);
        Assert.Equal(4, options.Transformation.MaxAttempts);
        Assert.Equal(500, options.Transformation.RetryBaseDelayMilliseconds);
        Assert.True(options.Transformation.EnableSdkLogging);
        Assert.True(options.Search.Categories);
        Assert.True(options.Search.QuerySuggestions);
        Assert.True(options.Search.IncludeUserToken);
        Assert.True(options.Search.VaryCacheByUserToken);
        Assert.Equal(3, options.Search.MinimumQueryLength);
        Assert.Equal(50, options.Search.MaxHitsPerPage);
        Assert.Equal(15, options.Search.Cache.DurationMinutes);
        Assert.False(options.Search.Cache.CacheEmptyResults);
        Assert.Equal(["filterOnly(categoryPageId)"], options.Indexing.AttributesForFaceting);
        Assert.Equal(["desc(ProductRanking)"], options.Indexing.ProductCustomRanking);
        Assert.Equal(["asc(SortOrder)"], options.Indexing.CategoryCustomRanking);
        Assert.Equal(["metafield:material"], options.Indexing.FacetAttributes);
        Assert.Equal("variantGroup:title", options.Indexing.VariantFacetAttributes["color"]);
        Assert.Equal("variant:title", options.Indexing.VariantFacetAttributes["size"]);
        var store = Assert.Single(options.Stores);
        Assert.True(store.EnableAvailabilityUpdates);
        Assert.Equal(["is"], store.LanguageSettings.QueryLanguages);
        Assert.Equal(["is"], store.LanguageSettings.IndexLanguages);
        Assert.True(store.LanguageSettings.RemoveStopWords);
        Assert.True(store.LanguageSettings.IgnorePlurals);
        Assert.Equal(["is"], store.LanguageSettings.IgnorePluralsLanguages);
        Assert.True(options.ContentIndexing.Enabled);
        Assert.Equal(AlgoliaOversizedRecordBehavior.Skip, options.ContentIndexing.OversizedRecords.Behavior);
        Assert.Equal(90_000, options.ContentIndexing.OversizedRecords.MaxSizeBytes);
        Assert.Equal("SearchIndex", options.ContentIndexing.Indexes.Single().IndexName);
        Assert.Equal(["desc(PublishedDate)"], options.ContentIndexing.Indexes.Single().CustomRanking);
        Assert.Equal("article", options.ContentIndexing.Indexes.Single().ContentTypes.Single().Alias);
        Assert.Equal(["title", "publishedAt|unix"], options.ContentIndexing.Indexes.Single().ContentTypes.Single().Properties);
    }

    [Fact]
    public void Binds_Algolia_Replica_And_Per_Store_Options()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ekom:Algolia:Indexing:SortedReplicas:0:Attribute"] = "price",
                ["Ekom:Algolia:Indexing:SortedReplicas:0:Direction"] = "Desc",
                ["Ekom:Algolia:Indexing:SortedReplicas:1:Attribute"] = "createdDateInDKUnix",
                ["Ekom:Algolia:Indexing:SortedReplicas:1:Type"] = "Standard",
                ["Ekom:Algolia:Stores:0:Alias"] = "Store",
                ["Ekom:Algolia:Stores:0:SearchableAttributes:0"] = "Title",
                ["Ekom:Algolia:Stores:0:SearchableAttributes:1"] = "Sku",
                ["Ekom:Algolia:Stores:0:Collections:Enabled"] = "true",
                ["Ekom:Algolia:Stores:0:Indexing:Enabled"] = "true",
                ["Ekom:Algolia:Stores:0:Indexing:Products"] = "false",
                ["Ekom:Algolia:Stores:0:Indexing:Variants"] = "true",
                ["Ekom:Algolia:Stores:0:Indexing:BatchSize"] = "250",
                ["Ekom:Algolia:Stores:0:Indexing:ProductProperties:0"] = "storeProperty",
                ["Ekom:Algolia:Stores:0:Indexing:ProductCustomRanking:0"] = "desc(ProductRanking)",
                ["Ekom:Algolia:Stores:0:Indexing:CategoryCustomRanking:0"] = "asc(SortOrder)",
                ["Ekom:Algolia:Stores:0:Indexing:SortedReplicas:0:Attribute"] = "title",
                ["Ekom:Algolia:Stores:1:Alias"] = "OtherStore",
                ["Ekom:Algolia:Stores:1:SearchableAttributes:0"] = "Summary",
            })
            .Build();

        var options = config.GetSection("Ekom:Algolia").Get<AlgoliaOptions>();

        Assert.NotNull(options);
        var sortedReplicas = options.Indexing.SortedReplicas.ToList();
        Assert.Equal(AlgoliaReplicaType.Virtual, sortedReplicas[0].Type);
        Assert.Equal(AlgoliaSortDirection.Desc, sortedReplicas[0].Direction);
        Assert.Equal(AlgoliaReplicaType.Standard, sortedReplicas[1].Type);
        var store = options.Stores.Single(x => x.Alias == "Store");
        Assert.Equal(["Title", "Sku"], store.SearchableAttributes);
        Assert.True(store.Collections.Enabled);
        Assert.NotNull(store.Indexing);
        Assert.True(store.Indexing.Enabled);
        Assert.False(store.Indexing.Products);
        Assert.True(store.Indexing.Categories);
        Assert.True(store.Indexing.Variants);
        Assert.Equal(250, store.Indexing.BatchSize);
        Assert.Equal(["storeProperty"], store.Indexing.ProductProperties);
        Assert.Equal(["desc(ProductRanking)"], store.Indexing.ProductCustomRanking);
        Assert.Equal(["asc(SortOrder)"], store.Indexing.CategoryCustomRanking);
        Assert.Equal("title", Assert.Single(store.Indexing.SortedReplicas).Attribute);
        var otherStore = options.Stores.Single(x => x.Alias == "OtherStore");
        Assert.Equal(["Summary"], otherStore.SearchableAttributes);
        Assert.False(otherStore.Collections.Enabled);
        Assert.Null(otherStore.Indexing);
        Assert.Equal("eu", options.TransformationRegion);
    }

    [Fact]
    public void Uses_Safe_Transformation_Defaults()
    {
        var options = new AlgoliaOptions
        {
            ApplicationId = "app-id",
            AdminApiKey = "admin-key",
            SearchApiKey = "search-key",
        };

        Assert.Equal(250, options.Transformation.MaxBatchSize);
        Assert.Equal(3, options.Transformation.MaxAttempts);
        Assert.Equal(1000, options.Transformation.RetryBaseDelayMilliseconds);
        Assert.False(options.Transformation.EnableSdkLogging);
        Assert.Empty(options.Indexing.ProductCustomRanking);
        Assert.Empty(options.Indexing.CategoryCustomRanking);
    }

    [Theory]
    [InlineData(null, "eu")]
    [InlineData("", "eu")]
    [InlineData(" EU ", "eu")]
    [InlineData("US", "us")]
    public void Resolves_Transformation_Region(string? configured, string expected)
    {
        var region = AlgoliaServiceCollectionExtensions.ResolveTransformationRegion(configured);

        Assert.Equal(expected, region);
    }

    [Fact]
    public void Rejects_Unsupported_Transformation_Region()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AlgoliaServiceCollectionExtensions.ResolveTransformationRegion("ap"));
    }

    [Fact]
    public void Does_Not_Validate_Transformation_Region_When_Plugin_Is_Disabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ekom:Algolia:Enabled"] = "false",
                ["Ekom:Algolia:ApplicationId"] = "app-id",
                ["Ekom:Algolia:AdminApiKey"] = "admin-key",
                ["Ekom:Algolia:TransformationRegion"] = "ap",
                ["Ekom:Algolia:Stores:0:Alias"] = "Store",
                ["Ekom:Algolia:Stores:0:Collections:Enabled"] = "true",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAlgolia();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ISearchClient>());
    }

    [Fact]
    public void Cache_Key_Changes_When_Store_Is_Invalidated()
    {
        var versions = new AlgoliaSearchCacheVersionProvider();
        var builder = new AlgoliaSearchCacheKeyBuilder(versions);
        var request = new AlgoliaSearchRequest
        {
            StoreAlias = "Store",
            Locale = "en-US",
            Currency = "USD",
            Query = new SearchForHits
            {
                Query = "chair",
                HitsPerPage = 20,
                Page = 1,
                Filters = "available:true"
            }
        };

        var first = builder.BuildProductsKey(request, request.Query, "primary.store.products.en-us.usd");

        versions.InvalidateStore(request.StoreAlias);

        var second = builder.BuildProductsKey(request, request.Query, "primary.store.products.en-us.usd");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Cache_Key_Changes_When_Query_Changes()
    {
        var versions = new AlgoliaSearchCacheVersionProvider();
        var builder = new AlgoliaSearchCacheKeyBuilder(versions);

        var first = builder.BuildProductsKey(
            new AlgoliaSearchRequest
            {
                StoreAlias = "Store",
                Query = new SearchForHits
                {
                    Query = "chair",
                    HitsPerPage = 20,
                    Page = 0
                }
            },
            new SearchForHits
            {
                Query = "chair",
                HitsPerPage = 20,
                Page = 0
            },
            "primary.store.products");

        var second = builder.BuildProductsKey(
            new AlgoliaSearchRequest
            {
                StoreAlias = "Store",
                Query = new SearchForHits
                {
                    Query = "table",
                    HitsPerPage = 20,
                    Page = 0
                }
            },
            new SearchForHits
            {
                Query = "table",
                HitsPerPage = 20,
                Page = 0
            },
            "primary.store.products");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Cache_Key_Changes_When_Query_User_Token_Changes()
    {
        var versions = new AlgoliaSearchCacheVersionProvider();
        var builder = new AlgoliaSearchCacheKeyBuilder(versions);
        var request = new AlgoliaSearchRequest
        {
            StoreAlias = "Store",
            Query = new SearchForHits
            {
                Query = "chair",
                HitsPerPage = 20,
                Page = 0
            }
        };

        var first = builder.BuildProductsKey(
            request,
            new SearchForHits
            {
                Query = "chair",
                HitsPerPage = 20,
                Page = 0,
                UserToken = "user-1"
            },
            "primary.store.products");

        var second = builder.BuildProductsKey(
            request,
            new SearchForHits
            {
                Query = "chair",
                HitsPerPage = 20,
                Page = 0,
                UserToken = "user-2"
            },
            "primary.store.products");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Category_Cache_Key_Uses_Category_Entity_Prefix()
    {
        var versions = new AlgoliaSearchCacheVersionProvider();
        var builder = new AlgoliaSearchCacheKeyBuilder(versions);
        var request = new AlgoliaSearchRequest
        {
            StoreAlias = "Store",
            Locale = "en-US",
            Query = new SearchForHits
            {
                Query = "candy",
                HitsPerPage = 10,
                Page = 0
            }
        };

        var key = builder.BuildCategoriesKey(request, request.Query, "primary.store.categories.en-us");

        Assert.Contains("categories", key);
    }

    [Fact]
    public void Content_Cache_Key_Changes_When_Query_Changes()
    {
        var versions = new AlgoliaSearchCacheVersionProvider();
        var builder = new AlgoliaSearchCacheKeyBuilder(versions);

        var first = builder.BuildContentKey(
            new AlgoliaContentSearchRequest
            {
                IndexName = "SearchIndex",
                Culture = "en-US",
                Query = new SearchForHits
                {
                    Query = "chair",
                    HitsPerPage = 20,
                    Page = 0
                }
            },
            new SearchForHits
            {
                Query = "chair",
                HitsPerPage = 20,
                Page = 0
            },
            "searchindex.prod.en-us");

        var second = builder.BuildContentKey(
            new AlgoliaContentSearchRequest
            {
                IndexName = "SearchIndex",
                Culture = "en-US",
                Query = new SearchForHits
                {
                    Query = "table",
                    HitsPerPage = 20,
                    Page = 0
                }
            },
            new SearchForHits
            {
                Query = "table",
                HitsPerPage = 20,
                Page = 0
            },
            "searchindex.prod.en-us");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Content_Index_Name_Uses_Base_Environment_And_Culture()
    {
        var resolver = new ContentIndexNameResolver(Options.Create(new AlgoliaOptions
        {
            ApplicationId = "app-id",
            AdminApiKey = "admin-key",
            SearchApiKey = "search-key",
            Environment = "Staging"
        }));

        var indexName = resolver.Resolve("SearchIndex", "en-US");

        Assert.Equal("searchindex.staging.en-us", indexName);
    }
}
