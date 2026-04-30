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
                ["Ekom:Algolia:Search:Cache:CacheEmptyResults"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<AlgoliaOptions>().Bind(config.GetSection("Ekom:Algolia"));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AlgoliaOptions>>().Value;

        Assert.Equal("search-key", options.SearchApiKey);
        Assert.True(options.Search.Categories);
        Assert.True(options.Search.QuerySuggestions);
        Assert.True(options.Search.IncludeUserToken);
        Assert.True(options.Search.VaryCacheByUserToken);
        Assert.Equal(3, options.Search.MinimumQueryLength);
        Assert.Equal(50, options.Search.MaxHitsPerPage);
        Assert.Equal(15, options.Search.Cache.DurationMinutes);
        Assert.False(options.Search.Cache.CacheEmptyResults);
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
}
