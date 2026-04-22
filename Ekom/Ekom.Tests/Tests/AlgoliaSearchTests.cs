using Algolia.Search.Models.Search;
using Ekom.Algolia;
using Ekom.Algolia.Models.Search;
using Ekom.Algolia.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
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
                ["Ekom:Algolia:Search:QuerySuggestions"] = "true",
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
        Assert.True(options.Search.QuerySuggestions);
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
    public void Store_Resolver_Returns_Absolute_Store_Domain()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var resolver = CreateStoreResolver("https://www.lyfja.is", loggerFactory);

        var store = resolver.Resolve("Lyfja");

        Assert.Equal("https://www.lyfja.is/", store.Domain);
    }

    [Fact]
    public void Store_Resolver_Warns_When_Store_Domain_Is_Invalid()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new TestLoggerProvider()));
        var resolver = CreateStoreResolver("www.lyfja.is", loggerFactory);

        var store = resolver.Resolve("Lyfja");

        Assert.Null(store.Domain);
        Assert.Contains(
            TestLoggerProvider.Entries,
            x => x.LogLevel == LogLevel.Warning
                && x.Message.Contains("Algolia store domain is invalid", StringComparison.Ordinal));
    }

    private static AlgoliaStoreResolver CreateStoreResolver(string? domain, ILoggerFactory loggerFactory)
    {
        var options = Options.Create(new AlgoliaOptions
        {
            ApplicationId = "app-id",
            AdminApiKey = "admin-key",
            SearchApiKey = "search-key",
            Stores =
            [
                new AlgoliaStoreOptions
                {
                    Alias = "Lyfja",
                    Domain = domain
                }
            ]
        });

        var serviceProvider = new Mock<IServiceProvider>();

        return new AlgoliaStoreResolver(options, serviceProvider.Object, loggerFactory.CreateLogger<AlgoliaStoreResolver>());
    }

    private sealed record TestLogEntry(LogLevel LogLevel, string Message);

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        public static List<TestLogEntry> Entries { get; } = [];

        public TestLoggerProvider()
        {
            Entries.Clear();
        }

        public ILogger CreateLogger(string categoryName) => new TestLogger();

        public void Dispose()
        {
        }

        private sealed class TestLogger : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                TestLoggerProvider.Entries.Add(new TestLogEntry(logLevel, formatter(state, exception)));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
